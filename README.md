# CodeWorks.Auth0Provider

```bash
dotnet add package CodeWorks.Auth0Provider
```

### Configure UserInfo (required)
In Auth0 you can create properties directly onto your userInfo Token. This is best accomplished with auth rules. In your Auth0 Dashboard be sure to enable RBAC and add in this custom rule

```javascript
//AUTH0 RULE
/**
 * Add common namespaced properties to userInfo
 */
function extendUserInfo(user, context, callback) {
    context.idToken = context.idToken || {};
    context.authorization = context.authorization || {};
    user.app_metadata = user.app_metadata || { };
    user.app_metadata.new = user.app_metadata.id ? false : true;
    user.app_metadata.id = user.app_metadata.id || generateId();

    for (const key in user.app_metadata) {
        context.idToken[key] = user.app_metadata[key];
    }
    context.idToken['roles'] = context.authorization.roles;
    context.idToken['permissions'] = context.authorization.permissions;
    context.idToken['user_metadata'] = user.user_metadata;
    
    if(!user.app_metadata.new){
        return callback(null, user, context);
    }
    delete user.app_metadata.new;
    auth0.users.updateAppMetadata(user.user_id, user.app_metadata)
        .then(function () {
            callback(null, user, context);
        })
        .catch(function (err) {
            callback(err);
        });
        
    function generateId() {
      let timestamp = (new Date().getTime() / 1000 | 0).toString(16);
      return timestamp + 'xxxxxxxxxxxxxxxx'.replace(/[x]/g, () => (
        Math.random() * 16 | 0).toString(16)).toLowerCase();
    }
}
```

### ConfigureKeyMap 
Properties that are added to Auth0 Tokens via rules can be retrieved with the following configuration. Namespaced properties are mapped to the top level class.  


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
