ASP.NET Core 6+ code snippets for JWT token validation. E.g. Okta JWT tokens.
Sources:
https://developer.okta.com/code/dotnet/jwt-validation/
https://jasonwatmore.com/post/2022/01/19/net-6-create-and-validate-jwt-tokens-use-custom-jwt-middleware
https://www.c-sharpcorner.com/article/jwt-validation-and-authorization-in-net-5-0/

Microsoft sources:
https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-7.0
Authentication schemes: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme?view=aspnetcore-7.0

### Prequisites
1. Register the Authentication service (plus authentication schemes) in program.cs or startup.cs
```
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});
```

2. Register the JWT Middleware that validates the Authorization token as part of the http request pipeline. You also want to register any dependent services (e.g. JWTManager) used by the Middleware with the DI container.
```
app.UseMiddleware<JwtMiddleware>();
```

### Authorization flow
1. Http Request with the Authorization token in the header is made
2. The **JWTMiddleware** (in the Request pipeline of asp.net) validates the Authorization token via the **JWTManager** class.
3. If the **JWTMiddleware** successfully validates the token, it will store one of the claims in the token (e.g. Id) in the request context.
4. The **Authorize** attribute (used to enforce auth in the controller) will check for the claim store in the request context from the previous step. If claim exists, then request is authorized to access the resource.