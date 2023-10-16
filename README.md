# CodeWorks.Auth0Provider

```bash
dotnet add package CodeWorks.Auth0Provider
```


***Startup.cs***
```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<CodeWorks.Utils.Auth0Provider>();
}
```

### UserInfoModel
Create you own UserInfo model and have the library extract and cast to your prefered class with generics.

***Profile.cs***
```c#
public class Profile
{
    public string Id { get; set; }  // Will be mapped to the id (uuid) from Auth0
    public string Name { get; set; }
    public string Email { get; set; }
    public string Picture { get; set; }
    public List<string> Roles { get; set; } = new List<string>(); // Will be mapped to the Roles from Auth0
    public List<string> Permissions { get; set; } = new List<string>(); // Will be mapped to the Permissions from Auth0
}
```

### HttpContext
extract the UserInfo in your controllers via HttpContext

***AccountController.cs***
```c#
public class AccountController : ControllerBase
  {
    // Uses Dependency IOC 
    private readonly Auth0Provider _auth0Provider;

    public AccountController(Auth0Provider auth0Provider)
    {
      _auth0Provider = auth0Provider;
    }

    [HttpGet]
    [Authorize] // Works with native controls
    public async Task<ActionResult<Account>> Get()
    {
      try
      {
        // retrieves userInfo from Auth0 or CacheControl
        Account userInfo = await _auth0Provider.GetUserInfoAsync<Account>(HttpContext);
        return Ok(userInfo);
      }
      catch (Exception e)
      {
        return BadRequest(e.Message);
      }
    }
  }
```
