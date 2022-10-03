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
    public class AwsApiGatewayIntegrationsOperationFilter : IOperationFilter
    {
        OpenAPIConfig _config;

        public AwsApiGatewayIntegrationsOperationFilter(OpenAPIConfig config)
        {
            _config = config;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (_config.AWSIntegration == null)
                return;

            // 1. Add "security" definitions
            var security = new OpenApiArray();

            // Attach cognito scopes and userpool to endpoints if Cognito is enabled on api
            Cognito cognito = _config.AWSIntegration.APIGateway.Cognito;
            if (cognito != null && cognito.Enabled)
            {
                if (cognito.Scopes == null)
                    throw new ArgumentNullException("Cognito scopes are missing in config section 'OpenApiConfig'");

                // Skip endpoints with attribute "AllowAnonymous"
                var attributes = context.ApiDescription.CustomAttributes();
                if (attributes != null && !attributes.Any(a => a.GetType() == typeof(AllowAnonymousAttribute)))
                {
                    var scopes = new OpenApiArray();
                    foreach(var s in cognito.Scopes)
                    {
                        scopes.Add(new OpenApiString(s));
                    }

                    security.Add(new OpenApiObject
                    {
                        [cognito.UserPool] = scopes,
                    });
                }
            }
           
            security.Add(new OpenApiObject
            {
                ["api_key"] = new OpenApiArray()
            });

            operation.Extensions.Add("security", security);

            if (!operation.Responses.Any(r => r.Key == "200"))
            {
                operation.Responses.Add("200", new OpenApiResponse
                {
                    Description = "Success"
                });
            }

            // 2. Add Api gateway integration
            // Get all the response types (200, 500, etc.) for the endpoint
            var responses = new OpenApiObject { };
            foreach(var r in operation.Responses)
            {
                if (!responses.ContainsKey(r.Key))
                {
                    responses[r.Key] = new OpenApiObject
                    {
                        ["statusCode"] = new OpenApiString(r.Key)
                    };
                }
            }

            if (_config.AWSIntegration.APIGateway.EnableCORS)
            {
                var httpCodes = new string[] { "200", "201", "400", "401", "403", "404", "500", "503", "504" };

                foreach(var code in httpCodes)
                {
                    var successObj = responses[code] as OpenApiObject;
                    successObj.Add("responseParameters", new OpenApiObject
                    {
                        ["method.response.header.Access-Control-Allow-Origin"] = new OpenApiString("'*'"),
                        ["method.response.header.Access-Control-Allow-Methods"] = new OpenApiString("'*'"),
                        ["method.response.header.Access-Control-Allow-Header"] = new OpenApiString("'*'")
                    });

                    var successResponse = operation.Responses[code];
                    successResponse.Headers = new Dictionary<string, OpenApiHeader>
                    {
                        { "Access-Control-Allow-Origin", new OpenApiHeader(){ Schema = new OpenApiSchema{ Type = "string"}, Description = "" } },
                        { "Access-Control-Allow-Methods", new OpenApiHeader(){ Schema = new OpenApiSchema{ Type = "string"}, Description = "" } },
                        { "Access-Control-Allow-Headers", new OpenApiHeader(){ Schema = new OpenApiSchema{ Type = "string"}, Description = "" } }
                    };
                }
            }

            // Define endpoint parameters for api gateway integration
            var requestParameters = new OpenApiObject { };
            foreach(var p in operation.Parameters)
            {
                if (p.In == ParameterLocation.Query)
                {
                    requestParameters["integration.request.querystring." + p.Name] = new OpenApiString("method.request.querystring." + p.Name);
                }
                else if (p.In == ParameterLocation.Path)
                {
                    requestParameters["integration.request.path." + p.Name] = new OpenApiString("method.request.path." + p.Name);
                }
                else if (p.In == ParameterLocation.Header)
                {
                    requestParameters["integration.request.header." + p.Name] = new OpenApiString("method.request.header." + p.Name);
                }
            }

            // Api integrations for each endpoint
            operation.Extensions.Add("x-amazon-apigateway-integration", new OpenApiObject
            {
                ["uri"] = new OpenApiString(_config.AWSIntegration.APIGateway.BaseUrl + context.ApiDescription.RelativePath),
                ["responses"] = responses,
                ["requestParameters"] = requestParameters,
                ["passthroughBehavior"] = new OpenApiString("when_no_match"),
                ["type"] = new OpenApiString("http"),
                ["httpMethod"] = new OpenApiString(context.ApiDescription.HttpMethod)
            });

            // AWS Api gateway doesn't like "string" responses. Remove schemas with "string" responses.
            foreach (var resp in operation.Responses)
            {
                var c = resp.Value.Content.Where(r => r.Value.Schema.Type == "string").ToList();
                foreach (var i in c)
                {
                    resp.Value.Content.Remove(i.Key);
                }
            }
        }
    }
}