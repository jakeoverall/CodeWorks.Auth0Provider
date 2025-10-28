using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CodeWorks.Utils
{
  public class Auth0Provider(HttpClient httpClient, IMemoryCache cache, IOptionsMonitor<JwtBearerOptions> jwtOptions)
  {
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptions = jwtOptions ?? throw new ArgumentNullException(nameof(jwtOptions));
    private readonly ConcurrentDictionary<string, Task<object>> _inFlightRequests = new();
    private readonly string _cacheKeyPrefix = "Auth0Provider_UserInfo_";

    public TimeSpan MaxWaitForInFlightRequest { get; set; } = TimeSpan.FromSeconds(10);

    public async Task<T> GetUserInfoAsync<T>(HttpContext ctx)
    {
      if (ctx?.User == null)
        throw new ArgumentNullException(nameof(ctx.User), "HttpContext.User is null");

      var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
      if (string.IsNullOrWhiteSpace(authHeader))
        return default!;

      var bearer = authHeader.Split(' ').LastOrDefault();
      if (string.IsNullOrWhiteSpace(bearer))
        return default!;

      var authority = _jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme)
                      ?.Authority?.TrimEnd('/')
                      ?? throw new Exception("JWT Authority not configured");

      var userId = GetUserId(ctx.User, authority);

      // Cache lookup
      if (_cache.TryGetValue($"{_cacheKeyPrefix}_{userId}", out CachedUser<T> cached)
          && cached.Expiration > DateTimeOffset.UtcNow)
        return cached.Data;

      _cache.Remove($"{_cacheKeyPrefix}_{userId}");

      var task = _inFlightRequests.GetOrAdd(userId, _ => FetchAndCacheAsync<T>(ctx, bearer, authority, userId));

      try
      {
        var completedTask = await Task.WhenAny(task, Task.Delay(MaxWaitForInFlightRequest));
        if (completedTask != task)
          throw new TimeoutException($"Waiting for user info request for {userId} exceeded {MaxWaitForInFlightRequest.TotalSeconds} seconds.");

        return (T)await task;
      }
      finally
      {
        _inFlightRequests.TryRemove(userId, out _);
      }
    }

    private async Task<object> FetchAndCacheAsync<T>(HttpContext ctx, string bearer, string authority, string userId)
    {
      var userInfoJson = await FetchNormalizedJsonString(ctx, bearer, authority);

      var entry = JsonSerializer.Deserialize<T>(
          userInfoJson,
          new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true }
      ) ?? throw new Exception("Failed to deserialize user info.");

      // Get token expiration
      var tokenHandler = new JwtSecurityTokenHandler();
      var jwt = tokenHandler.ReadJwtToken(bearer);
      var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
      if (!long.TryParse(expClaim, out long expUnix))
        throw new Exception("Token 'exp' claim missing or invalid.");

      var tokenExp = DateTimeOffset.FromUnixTimeSeconds(expUnix);

      var cacheEntry = new CachedUser<T> { Data = entry, Expiration = tokenExp };
      _cache.Set($"{_cacheKeyPrefix}_{userId}", cacheEntry, new MemoryCacheEntryOptions
      {
        AbsoluteExpiration = tokenExp
      });

      return entry!;
    }

    private async Task<string> FetchNormalizedJsonString(HttpContext ctx, string bearer, string authority)
    {
      // Try to get from claim, else default to standard /userinfo endpoint
      var requestUrl =
          ctx.User.FindFirst(c => c.Value.EndsWith("userinfo"))?.Value
          ?? $"{authority}/userinfo";

      using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      using var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
        throw new Exception($"Auth0 request failed: {response.ReasonPhrase}");

      using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

      var normalizedDict = new Dictionary<string, JsonElement>();
      foreach (var prop in doc.RootElement.EnumerateObject())
      {
        var key = prop.Name.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? prop.Name[(prop.Name.LastIndexOf('/') + 1)..]
            : prop.Name;

        normalizedDict[key] = prop.Value.Clone();
      }

      return JsonSerializer.Serialize(normalizedDict);
    }

    private static string GetUserId(ClaimsPrincipal user, string authority)
    {
      string[] claimKeys =
      {
        "id",
        $"{authority}/id",
        "sub",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
      };

      return user.Claims
        .FirstOrDefault(c => claimKeys.Contains(c.Type, StringComparer.OrdinalIgnoreCase))
        ?.Value
        ?? throw new Exception("User identifier claim not found.");
    }

    private class CachedUser<T>
    {
      public T Data { get; set; } = default!;
      public DateTimeOffset Expiration { get; set; }
    }
  }
}
