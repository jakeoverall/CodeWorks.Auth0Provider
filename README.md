# CodeWorks.Auth0Provider

```bash
dotnet add package CodeWorks.Auth0Provider
```

### Configure UserInfo
In auth0 you can create namespaced properties directly onto your userInfo Token. This is best accomplished with auth rules. Properties that are added to Auth0 Tokens via rules can be retrieved with the following configuration

***Startup.cs***
```c#
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // ...
    Auth0ProviderExtension.ConfigureKeyMap(new List<string>() { "id", "roles", "permissions" });
    // ...
}
```

### UserInfoModel
Create you own UserInfo model and have the library extract and cast to your prefered class with generics.

***Profile.cs***
```c#
  public class Profile
  {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Picture { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
    public List<string> Permissions { get; set; } = new List<string>();
  }
```

### HttpContext
extract the UserInfo in your controllers via HttpContext

***SomeController.cs***
```c#
[HttpGet]
[Authorize]
public async Task<ActionResult<Profile>> GetProfile(string id)
{
    try
    {
        Profile userInfo = await HttpContext.GetUserInfoAsync<Profile>();
        return Ok(userInfo)
    }
    catch (Exception e)
    {
        return BadRequest(e.Message);
    }
}
```