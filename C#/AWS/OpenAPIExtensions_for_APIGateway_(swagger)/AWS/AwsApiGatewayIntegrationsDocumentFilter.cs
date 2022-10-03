using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System;

namespace PB.ApiExtensions.OpenApi.AWSIntegrations
{
    public class AwsApiGatewayIntegrationsDocumentFilter : IDocumentFilter
    {
        OpenAPIConfig _config;

        public AwsApiGatewayIntegrationsDocumentFilter(OpenAPIConfig config)
        {
            _config = config;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            //swaggerDoc.Info.Title = _config.Title;
            //swaggerDoc.Info.Version = _config.Version;
            //swaggerDoc.Info.Description = _config.Description;

            if (_config.AWSIntegration == null)
                return;

            if (_config.AWSIntegration.Enabled)
            {
                APIGateway apiGateway = _config.AWSIntegration.APIGateway;

                if (apiGateway == null || string.IsNullOrWhiteSpace(apiGateway.ApiGatewayUrl) || string.IsNullOrWhiteSpace(apiGateway.BaseUrl))
                    throw new ArgumentNullException("APIGatway config section is missing or invalid");

                var serverSection = swaggerDoc.Servers.FirstOrDefault();
                if (serverSection != null)
                    serverSection.Url = _config.AWSIntegration.APIGateway.ApiGatewayUrl;

                Cognito cognito = _config.AWSIntegration.APIGateway.Cognito;

                if (cognito != null && cognito.Enabled)
                {
                    if (string.IsNullOrWhiteSpace(cognito.UserPool) || string.IsNullOrWhiteSpace(cognito.Authorizer))
                        throw new ArgumentNullException("Cognito config section is missing required values: Userpool and Authorizer.");

                    swaggerDoc.Components.SecuritySchemes.Add(cognito.UserPool, new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header
                    });

                    var sc = swaggerDoc.Components.SecuritySchemes.FirstOrDefault(s => s.Key == cognito.UserPool);
                    if (!sc.Equals(default(KeyValuePair<string, OpenApiSecurityScheme>)))
                    {
                        sc.Value.Extensions.Add("x-amazon-apigateway-authtype", new OpenApiString("cognito_user_pools"));
                        sc.Value.Extensions.Add("x-amazon-apigateway-authorizer", new OpenApiObject
                        {
                            ["providerARNs"] = new OpenApiArray
                            {
                                new OpenApiString(cognito.Authorizer)
                            },
                            ["type"] = new OpenApiString("cognito_user_pools")
                        });
                    }
                }

                swaggerDoc.Components.SecuritySchemes.Add("api_key", new OpenApiSecurityScheme
                {
                    Name = "x-api-key",
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header
                });
            }

            if (_config.AWSIntegration.APIGateway.EnableCORS && swaggerDoc.Paths != null)
            {
                foreach (var path in swaggerDoc.Paths)
                {
                    var operation = new OpenApiOperation();
                    operation.Responses["200"] = new OpenApiResponse
                    {
                        Headers = new Dictionary<string, OpenApiHeader>
                        {
                            { "Access-Control-Allow-Origin", new OpenApiHeader(){ Schema = new OpenApiSchema{ Type = "string"}, Description = "" } },
                            { "Access-Control-Allow-Methods", new OpenApiHeader(){ Schema = new OpenApiSchema{ Type = "string"}, Description = "" } },
                            { "Access-Control-Allow-Headers", new OpenApiHeader(){ Schema = new OpenApiSchema{ Type = "string"}, Description = "" } }
                        }
                    };

                    operation.Extensions.Add("x-amazon-apigateway-integration", new OpenApiObject
                    {
                        ["uri"] = new OpenApiString(_config.AWSIntegration.APIGateway.BaseUrl + path.Key.Substring(1)),
                        ["type"] = new OpenApiString("mock"),
                        ["contentHandling"] = new OpenApiString("CONVERT_TO_TEXT"),
                        ["requestTemplates"] = new OpenApiObject
                        {
                            ["application/json"] = new OpenApiString("{ \"statusCode\": 200 }")
                        },
                        ["responses"] = new OpenApiObject
                        {
                            ["200"] = new OpenApiObject 
                            {
                                ["statusCode"] = new OpenApiInteger(200),
                                ["contentHandling"] = new OpenApiString("CONVERT_TO_TEXT"),
                                ["responseParameters"] = new OpenApiObject
                                {
                                    ["method.response.header.Access-Control-Allow-Headers"] = new OpenApiString("'*'"),
                                    ["method.response.header.Access-Control-Allow-Methods"] = new OpenApiString("'OPTIONS,POST,GET,PUT,DEL'"),
                                    ["method.response.header.Access-Control-Allow-Origin"] = new OpenApiString("'*'")
                                }
                            }
                        }
                    });

                    path.Value.AddOperation(OperationType.Options, operation);
                }
            }
        }
    }
}