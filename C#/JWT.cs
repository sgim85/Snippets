// ******************* BEGIN: Action method return JWT *******************
/// <summary>
/// Get a test jwt token for Dev testing. To test the endpoints, authorize access by clicking the "Authorize" button above and add the value "Bearer {Token}".
/// This feature is disabled in Production (returns 404)
/// </summary>
/// <param name="userID">Public Secure Id - (OIDC JWT uid). Can be any alphanumeric string.</param>
/// <returns>JWT token</returns>
[FeatureGate("EnableDebugActions")]
[AllowAnonymous]
[HttpGet("jwttoken")]
[ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
[ProducesResponseType((int)HttpStatusCode.NotFound)]
public IActionResult GenerateJwtToken([Required] string userID)
{
    if (_env.IsProduction())
        return NotFound("This call is restricted to dev env");

    // generate token that is valid for 2 days
    var tokenHandler = new JwtSecurityTokenHandler();

    var key = Encoding.ASCII.GetBytes(JwtMiddleware.OIDC_Secret_Key); // for dev testing

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[] { new Claim("uid", userID) }),
        Expires = DateTime.UtcNow.AddDays(2),
        Issuer = Environment.GetEnvironmentVariable("OIDC_Issuer"),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var strToken = tokenHandler.WriteToken(token);
    return Ok(strToken);
}
// ******************* END: Action method return JWT *******************

// ******************* BEGIN: JWT Middleware validation (validate token in request header) *******************
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace CXP.Common.Middleware
{
    /// <summary>
    /// .NET middleware for handling JWT validation
    /// </summary>
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly JwtManager _jwtManager;
        public static readonly string OIDC_Secret_Key = "I6UP5t80hxwOW6RNIpfg3kjyvBpdXnNn";

        public JwtMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;

            // It is important that JwtManager is instantiated once. This condition is met since middleware is instantiated once.
            _jwtManager = new JwtManager(config);
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token != null)
            {
                JwtSecurityToken jwtSecurityToken = null;
                try
                {
                    jwtSecurityToken = await _jwtManager.ValidateToken(token);
                }
                catch(Exception) { }

                if (jwtSecurityToken != null)
                {
                    // attach user to context on successful jwt validation
                    context.Items["Authenticated"] = true;

                    var uid = jwtSecurityToken.Claims.FirstOrDefault(p => p.Type == "uid");
                    context.Items["Public_Secure_Id"] = (uid != null ? uid.Value : string.Empty);
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Contains facilities for JWT validation for Okta-based JWTs (since Public Secure is backed by Okta)
    /// https://developer.okta.com/code/dotnet/jwt-validation/
    /// </summary>
    public class JwtManager
    {
       // private readonly ILogger<JwtManager> _logger;
        private readonly IConfiguration _config;

        public JwtManager(IConfiguration config)
        {
            //_logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Okta signs JWTs using asymmetric encryption (RS256), and publishes the public signing keys in a JWKS (JSON Web Key Set) as part of the OAuth 2.0 and OpenID Connect discovery documents. The signing keys are rotated on a regular basis. The first step to verify a signed JWT is to retrieve the current signing keys.
        /// The OpenIdConnectConfigurationRetriever class will download and parse the discovery document to get the key set. Can be used in conjunction with the ConfigurationManager.
        /// Instatiate ConfigurationManager once (singletone). E.g. register JwtManager as a singleton with the DI container.
        /// </summary>
        private ConfigurationManager<OpenIdConnectConfiguration> _configManager = null;
        private ConfigurationManager<OpenIdConnectConfiguration> ConfigManager
        {
            get
            {
                if (_configManager != null)
                    return _configManager;

                _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    JwtIssuer + "/.well-known/oauth-authorization-server",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever());

                return _configManager;
            }
        }

        public string JwtIssuer => Environment.GetEnvironmentVariable("OIDC_Issuer")!;

        /// <summary>
        /// Called by the JWTMiddleware (during the http Request pipeline flow) to verify an Authorization token.
        /// </summary>
        /// <param name="token">Encrypted Authorization token (JWT) passed in the requeste header</param>
        /// <returns>A JwtSecurityToken retrieved after deserializing the token</returns>
        public async Task<JwtSecurityToken> ValidateToken(string token)
        {
            return await ValidateToken(token, JwtIssuer, ConfigManager);
        }

        private async Task<JwtSecurityToken> ValidateToken(
            string token,
            string issuer,
            IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));

            if (string.IsNullOrEmpty(issuer)) throw new ArgumentNullException(nameof(issuer));

            ICollection<SecurityKey> signingKeys = null;
            try
            {
                var discoveryDocument = await configurationManager.GetConfigurationAsync(ct);
                signingKeys = discoveryDocument.SigningKeys;
            }
            catch(Exception){ }

            var key = Encoding.ASCII.GetBytes(JwtMiddleware.OIDC_Secret_Key); // for dev testing

            var validationParameters = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                IssuerSigningKey = new SymmetricSecurityKey(key), // For dev testing using dev JWTs (see GenerateJwtToken() in AccountController)
                ValidateLifetime = true,
                // Allow for some drift in server time
                // (a lower value is better; we recommend two minutes or less)
                ClockSkew = TimeSpan.FromMinutes(2),

                //If validating access tokens, you should verify that the aud (audience) claim equals the audience that is configured for your Authorization Server in the Admin Console.
                //If validating an ID token, you should verify that the aud (Audience) claim equals the Client ID of the current application.
                //ValidateAudience = true,
                //ValidAudience = _config["Auth:Jwt:Audience"]
                ValidateAudience = false
            };

            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParameters, out var rawValidatedToken);

            return (JwtSecurityToken)rawValidatedToken;
        }
    }
}
// ******************* END: JWT Middleware validation (validate token in request header) *******************
