//JWT Middleware to plug into the asp.net core Request pipeline
// In program.cs (or start.cs), add this: app.UseMiddleware<JwtMiddleware>();

namespace Profile.API.Auth
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly JwtManager _jwtManager;

        public JwtMiddleware(RequestDelegate next, IConfiguration config, ILogger<JwtManager> logger, JwtManager jwtManager)
        {
            _next = next;
            _jwtManager = jwtManager ?? throw new ArgumentNullException(nameof(jwtManager));
        }

		// Function that is invoked during the request pipeline
        public async Task Invoke(HttpContext context)
        {
			// Retrieve token from "Authorization" header
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
			
			// validate token via the JWTManager class, ValidateToken function
            var jwtSecurityToken = await _jwtManager.ValidateToken(token);
			
			// If validation passes, set a context variable to use for checking if client is validated
            if (jwtSecurityToken != null)
            {
                // attach user to context on successful jwt validation
                context.Items["JwtID"] = jwtSecurityToken.Claims.First(x => x.Type == "id").Value;
            }

            await _next(context);
        }
    }
}
