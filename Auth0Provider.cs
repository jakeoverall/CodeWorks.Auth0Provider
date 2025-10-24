using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace CodeWorks.Utils
{
  public class Auth0Provider(HttpClient httpClient, IMemoryCache cache)
  {
    public TimeSpan MaxWaitForInFlightRequest { get; set; } = TimeSpan.FromSeconds(10);

    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ConcurrentDictionary<string, Task<object>> _inFlightRequests = new();
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly string _cacheKeyPrefix = "Auth0Provider_UserInfo_";

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

      var userId = ctx.User.FindFirst("id")?.Value ?? ctx.User.FindFirst("sub")?.Value
                   ?? throw new Exception("User 'sub' claim not found");

      // Check cache
      if (_cache.TryGetValue($"{_cacheKeyPrefix}_{userId}", out CachedUser<T> cached) && cached.Expiration > DateTimeOffset.UtcNow)
        return cached.Data;

      _cache.Remove($"{_cacheKeyPrefix}_{userId}");

      // Handle in-flight requests
      var task = _inFlightRequests.GetOrAdd(userId, _ => FetchAndCacheAsync<T>(ctx, bearer, userId));

      try
      {
        var completedTask = await Task.WhenAny(task, Task.Delay(MaxWaitForInFlightRequest));
        if (completedTask != task)
          throw new TimeoutException(
              $"Waiting for user info request for {userId} exceeded {MaxWaitForInFlightRequest.TotalSeconds} seconds.");

        return (T)await task;
      }
      finally
      {
        _inFlightRequests.TryRemove(userId, out _);
      }
    }

    private async Task<object> FetchAndCacheAsync<T>(HttpContext ctx, string bearer, string userId)
    {
      var userInfoJson = await FetchNormalizedJsonString(ctx, bearer);

      // Deserialize case-insensitive
      var entry = JsonSerializer.Deserialize<T>(
          userInfoJson,
          new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, }
      ) ?? throw new Exception("Failed to deserialize user info.");

      // Get token expiration from JWT "exp" claim
      var tokenHandler = new JwtSecurityTokenHandler();
      var jwt = tokenHandler.ReadJwtToken(bearer);
      var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
      if (!long.TryParse(expClaim, out long expUnix))
        throw new Exception("Token 'exp' claim missing or invalid.");

      var tokenExp = DateTimeOffset.FromUnixTimeSeconds(expUnix);

      // Cache until token expiration
      var cacheEntry = new CachedUser<T> { Data = entry, Expiration = tokenExp };
      _cache.Set($"{_cacheKeyPrefix}_{userId}", cacheEntry, new MemoryCacheEntryOptions
      {
        AbsoluteExpiration = tokenExp
      });

      return entry!;
    }

    private async Task<string> FetchNormalizedJsonString(HttpContext ctx, string bearer)
    {
      var requestUrl = ctx.User.FindFirst(c => c.Value.EndsWith("userinfo"))?.Value
                       ?? throw new Exception("UserInfo endpoint not found in claims.");

      using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      using var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
        throw new Exception($"Auth0 request failed: {response.ReasonPhrase}");

      using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

      // Normalize keys, overwrite duplicates
      var normalizedDict = new Dictionary<string, JsonElement>();
      foreach (var prop in doc.RootElement.EnumerateObject())
      {
        var key = prop.Name.StartsWith("http")
            ? prop.Name.Substring(prop.Name.LastIndexOf('/') + 1)
            : prop.Name;

        normalizedDict[key] = prop.Value.Clone();
      }

      return JsonSerializer.Serialize(normalizedDict);
    }

    private class CachedUser<T>
    {
      public T Data { get; set; } = default!;
      public DateTimeOffset Expiration { get; set; }
    }
  }
}
