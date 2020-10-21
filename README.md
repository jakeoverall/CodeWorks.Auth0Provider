# CodeWorks.Auth0Provider

```bash
dotnet add package CodeWorks.Auth0Provider
```

### Configure UserInfo (required)
In Auth0 you can create namespaced properties directly onto your userInfo Token. This is best accomplished with auth rules. In your Auth0 Dashboard be sure to enable RBAC and add in this custom rule

```javascript
//AUTH0 RULE
/**
 * Add common namespaced properties to userInfo, 
 * note auth0 will strip any non namespaced properties
 */
function extendUserInfo(user, context, callback) {
    const uuid = require('uuid@3.3.2');
    const namespace = 'https://YOURDOMAINHERE.auth0.com';
    context.idToken = context.idToken || {};
    context.authorization = context.authorization || {};
    user.app_metadata = user.app_metadata || { new: true };
    user.app_metadata.id = user.app_metadata.id || uuid();

    // Enabled to map app_metadata properties to top level Profile
    for (const key in user.app_metadata) {
        context.idToken[`${namespace}/${key}`] = user.app_metadata[key];
    }
    context.idToken[`${namespace}/roles`] = context.authorization.roles;
    context.idToken[`${namespace}/permissions`] = context.authorization.permissions;
    context.idToken[`${namespace}/user_metadata`] = user.user_metadata;
    
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
}
```

### ConfigureKeyMap 
Properties that are added to Auth0 Tokens via rules can be retrieved with the following configuration. Namespaced properties are mapped to the top level class.  


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