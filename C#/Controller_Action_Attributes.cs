// Custom Authorize attribute that verfifies if request was authorized via a JWT in the header (see JWT.cs for jwt middleware logic)
 [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
 public class JWTAuthorizeAttribute : Attribute, IAuthorizationFilter
 {
     public void OnAuthorization(AuthorizationFilterContext context)
     {
         // skip authorization if action is decorated with [AllowAnonymous] attribute
         var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any();
         if (allowAnonymous)
             return;

         // "Authenticated" set in the JwtMiddleware class
         var isAuthenticated = context.HttpContext.Items["Authenticated"] != null ? (bool)context.HttpContext.Items["Authenticated"] : false;
         if (!isAuthenticated)
             context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
     }
 }

// Custom Authorize attribut to let JWT auth middleware to extract some value. Attribute will allow anony access too.
/// <summary>
/// Use this attribute to simply extract the Public Secure Id from JwtMiddlware.cs and save it in the context. In otherwords, behaves like Unauthorized attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SoftFailJWTAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Will invoke the JWTMiddlware to simply extract the Public Secure Id
    }
}
