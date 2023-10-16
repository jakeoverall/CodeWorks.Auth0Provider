using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace CodeWorks.Utils
{
  public class Auth0Provider
  {
    public int TTL { get; set; } = 60 * 60;
    public MemoryCacheOptions MemoryCacheOptions { get; set; } = new MemoryCacheOptions()
    {
      ExpirationScanFrequency = TimeSpan.FromMinutes(15),
    };

    private readonly MemoryCache _cache;

    public async Task<T> GetUserInfoAsync<T>(HttpContext ctx)
    {
      if (!ctx.Request.Headers.ContainsKey("Authorization"))
      {
        return default(T);
      }
      var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault();
      T data;

      if (_cache.TryGetValue(bearer, out data))
      {
        return data;
      }

      var userInfo = await FetchFromAuth0(ctx, bearer);
      T entry = userInfo.ToObject<T>();
      _cache.Set(bearer, entry, GetMemoryCacheEntryOptions());

      return userInfo.ToObject<T>();
    }
    private async Task<dynamic> FetchFromAuth0(HttpContext ctx, string bearer)
    {
      var client = new HttpClient();
      var requestUrl = ctx.User.FindFirst(c => c.Value.EndsWith("userinfo")).Value;
      var iss = ctx.User.FindFirst(c => c.Type == "iss").Value;
      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders.Add("authorization", bearer);
      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      HttpResponseMessage response = await client.GetAsync(requestUrl);

      if (!response.IsSuccessStatusCode)
      {
        throw new Exception(response.ReasonPhrase);
      }

      // Handle the UserInfo Object
      var res = await response.Content.ReadAsStringAsync();
      dynamic data = JObject.Parse(res);
      dynamic userInfo = JObject.Parse(res);

      foreach (JProperty key in data.Properties())
      {
        if(key.Name.StartsWith("http")){
          var prop = key.Name.Substring(key.Name.LastIndexOf('/')+1);
          userInfo[prop] = data[key.Name];
        }
      }
      return userInfo;
    }

    public virtual MemoryCacheEntryOptions GetMemoryCacheEntryOptions()
    {
      return new MemoryCacheEntryOptions()
      {
        SlidingExpiration = TimeSpan.FromSeconds(TTL),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
      };
    }


    public Auth0Provider()
    {
      _cache = new MemoryCache(MemoryCacheOptions);
    }

    public Auth0Provider(MemoryCacheOptions memoryCacheOptions)
    {
      _cache = new MemoryCache(memoryCacheOptions);
    }

  }
}
