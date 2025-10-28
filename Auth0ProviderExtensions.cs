using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace CodeWorks.Utils;

public static class Auth0ProviderExtensions
{
  /// <summary>
  /// Registers Auth0Provider with all required dependencies.
  /// </summary>
  public static IServiceCollection AddAuth0Provider(this IServiceCollection services)
  {
    services.AddMemoryCache();
    services.AddHttpClient<Auth0Provider>(o =>
    o.DefaultRequestHeaders.Accept.Add(
      new MediaTypeWithQualityHeaderValue("application/json")
    ));
    return services;
  }
}
