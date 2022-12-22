using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Profile.API.Auth
{
    /// <summary>
    /// JWT Manager provides facilities for JWT token validation. Based on the okta OpenIdConnect flow.
    /// https://developer.okta.com/code/dotnet/jwt-validation/
    /// https://jasonwatmore.com/post/2022/01/19/net-6-create-and-validate-jwt-tokens-use-custom-jwt-middleware
    /// </summary>
    public class JwtManager
    {
        private readonly ILogger<JwtManager> _logger;
        private readonly IConfiguration _config;

        public JwtManager(IConfiguration config, ILogger<JwtManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

		/// <summary>
        /// Okta signs JWTs using asymmetric encryption (RS256), and publishes the public signing keys in a JWKS (JSON Web Key Set) as part of the OAuth 2.0 and OpenID Connect discovery documents. 
		/// The signing keys are rotated on a regular basis. The first step to verify a signed JWT is to retrieve the current signing keys.
        /// The OpenIdConnectConfigurationRetriever class (in the Microsoft.IdentityModel.Protocols.OpenIdConnect package) will download and parse the discovery document to get the key set. Can be used in conjunction with the ConfigurationManager.
        /// Instatiate ConfigurationManager once (singletone). E.g. register JwtManager as a singleton with the DI container.
        /// </summary>
        private ConfigurationManager<OpenIdConnectConfiguration> _configManager = null;
        public ConfigurationManager<OpenIdConnectConfiguration> ConfigManager
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

        public string JwtIssuer
        {
            get
            {
                return _config["Auth:Jwt:Issuer"];
            }
        }

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
            //if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            //if (string.IsNullOrEmpty(issuer)) throw new ArgumentNullException(nameof(issuer));

            //var discoveryDocument = await configurationManager.GetConfigurationAsync(ct);
            //var signingKeys = discoveryDocument.SigningKeys;

            var key = Encoding.ASCII.GetBytes(_config["Auth:Jwt:Key"]); // for dev testing

            var validationParameters = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateIssuerSigningKey = true,
                //IssuerSigningKeys = signingKeys, // E.g. for Okta
                IssuerSigningKey = new SymmetricSecurityKey(key), // for dev testing
                ValidateLifetime = true,
                // Allow for some drift in server time
                // (a lower value is better; we recommend two minutes or less)
                ClockSkew = TimeSpan.FromMinutes(2),

                //If you are validating access tokens, you should verify that the aud (audience) claim equals the audience that is configured for your Authorization Server in the Admin Console.
                //If validating an ID token, you should verify that the aud (Audience) claim equals the Client ID of the current application.
                ValidateAudience = true,
                ValidAudience = _config["Auth:Jwt:Audience"],
            };

            try
            {
                var principal = new JwtSecurityTokenHandler()
                    .ValidateToken(token, validationParameters, out var rawValidatedToken);

                return (JwtSecurityToken)rawValidatedToken;
            }
            catch (SecurityTokenValidationException ex)
            {
                _logger.LogError(ex, nameof(ex));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Fatal error during security token validation");
            }
            return null;
        }
    }
}
