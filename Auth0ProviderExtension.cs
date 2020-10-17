using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace CodeWorks.Auth0Provider
{
    public static class Auth0ProviderExtension
    {
        private static List<string> _KeyMap = new List<string>();

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
                return default(T);
            }
            var bearer = ctx.Request.Headers["Authorization"].FirstOrDefault();
            var client = new HttpClient();
            var requestUrl = ctx.User.FindFirst(c => c.Value.EndsWith("userinfo")).Value;
            var iss = ctx.User.FindFirst(c => c.Type == "iss").Value;
            iss = iss.Replace('.', ':');
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("authorization", bearer);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = await client.GetAsync(requestUrl);
            if (response.IsSuccessStatusCode)
            {
                var res = await response.Content.ReadAsStringAsync();
                dynamic data = JObject.Parse(res);
                foreach (var key in _KeyMap)
                {
                    if (data[iss + key] != null)
                    {
                        data[key] = data[iss + key];
                    }
                }
                var d = data.ToObject<T>();
                return d;
            }
            return default(T);
        }
    }

}
