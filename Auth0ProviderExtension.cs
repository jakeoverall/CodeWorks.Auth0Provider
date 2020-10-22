using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace CodeWorks.Auth0Provider
{
   public static class Auth0ProviderExtension
  {
    public static int TTL = 60 * 1000;
    private static List<string> _KeyMap = new List<string>() { "id" };
    private static MemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
    {
    });
    public static void ConfigureKeyMap(List<string> keys)
    {
      keys.ForEach(key =>
      {
        _KeyMap.Add(key);
      });
    }
    public static async Task<T> GetUserInfoAsync<T>(this HttpContext ctx)
    {
      if (!ctx.Request.Headers.ContainsKey("Authorization"))
      {
        throw new Exception("No Bearer Token Provided");
      }
      var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault();
      T data;
      if (_cache.TryGetValue(bearer, out data))
      {
        return data;
      }
      var userInfo = await FetchFromAuth0(ctx, bearer);
      T entry = userInfo.ToObject<T>();
      _cache.Set(bearer, entry, new MemoryCacheEntryOptions()
      {
        SlidingExpiration = TimeSpan.FromMilliseconds(TTL)
      });
      return entry;
    }
    private static async Task<dynamic> FetchFromAuth0(HttpContext ctx, string bearer)
    {
      var client = new HttpClient();
      var requestUrl = ctx.User.FindFirst(c => c.Value.EndsWith("userinfo")).Value;
      var iss = ctx.User.FindFirst(c => c.Type == "iss").Value;
      iss = iss.Replace('.', ':');
      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders.Add("authorization", bearer);
      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      HttpResponseMessage response = await client.GetAsync(requestUrl);
      if (!response.IsSuccessStatusCode)
      {
        throw new Exception("Unable to retrieve userInfo from Bearer Token");
      }
      var res = await response.Content.ReadAsStringAsync();
      dynamic data = JObject.Parse(res);
      foreach (var key in _KeyMap)
      {
        if (data[iss + key] != null)
        {
          data[key] = data[iss + key];
        }
      }
      return data;
    }
  }
}
