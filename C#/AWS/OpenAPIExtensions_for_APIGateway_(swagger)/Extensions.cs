using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;
using PB.ApiExtensions.OpenApi.AWSIntegrations;

namespace PB.ApiExtensions.OpenApi
{
    public static class Extensions
    {
        public static IServiceCollection AddOpenApiIntegrations(this IServiceCollection services, IConfigurationSection configSection)
        {
            var config = new OpenAPIConfig();
            configSection.Bind(config);

            if (string.IsNullOrWhiteSpace(config.Title))
                throw new ArgumentNullException("API Title required. Make sure appsettings.json has section 'OpenAPIConfig'");

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(config.Version, new OpenApiInfo
                {
                    Title = config.Title,
                    Version = config.Version,
                    Description = config.Description
                });

                options.DocumentFilter<AwsApiGatewayIntegrationsDocumentFilter>(config);
                options.OperationFilter<AwsApiGatewayIntegrationsOperationFilter>(config);
            });

            return services;
        }
    }
}
