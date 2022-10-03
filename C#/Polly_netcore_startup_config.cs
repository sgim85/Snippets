using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using PB.Logging;
using PB.Email;
using PB.Monitoring;
using System;
using System.IO;
using Email.API.MessageQueue;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Net.Http;
using System.Threading.Tasks;

namespace Email.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddPorterBusinessLogging(Configuration.GetSection("PorterBusinessLogging"));
            services.AddEmailOptions(Configuration.GetSection("EmailOptions"));
            services.AddPorterBusinessMonitoring("Email.API");

			// *********************************** POLLY **************************************************
            IAsyncPolicy<HttpResponseMessage> httpWaitAndRetryPolicy =
                 HttpPolicyExtensions
                        .HandleTransientHttpError()
                        .Or<TimeoutRejectedException>()
                        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                            onRetry: (result, timeSpan, retryAttempt, context) =>
                            {
                                // stuff to do during retry
                            });

            IAsyncPolicy<HttpResponseMessage> fallbackPolicy =
                Policy.Handle<Exception>().OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .FallbackAsync(
                        fallbackAction: (response, context, cancellationToken) =>
                        {
                            if (response.Exception != null)
                                throw response.Exception;
                            else
                                throw new Exception($"Exception occured with StatusCode {response.Result.StatusCode}. URL {response.Result.RequestMessage.RequestUri}");
                        },
                        onFallbackAsync: (response, context) =>
                        {
                            if (response.Exception != null && response.Exception.Data != null)
                                response.Exception.Data.Add("IsTransientException", true);
                            return Task.CompletedTask;
                        });

            IAsyncPolicy<HttpResponseMessage> httpRetryAndFallbackWrapper = Policy.WrapAsync(fallbackPolicy, httpWaitAndRetryPolicy);

            services.AddHttpClient<EmailManager, EmailManager>()
            .AddPolicyHandler(httpRetryAndFallbackWrapper)
            .AddPolicyHandler((timeout) => Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30)));

            // *******This code replaced by "httpRetryAndFallbackWrapper PolicyHandler" above*******
            // HttpClient for 3rd party email service
            //services.AddHttpClient<EmailManager, EmailManager>()
            //    .AddPolicyHandler((service, request) =>
            //        HttpPolicyExtensions
            //            .HandleTransientHttpError()
            //            .Or<TimeoutRejectedException>()
            //            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            //                onRetry: (result, timeSpan, retryAttempt, context) => 
            //                {
            //                    result.Exception.Data.Add("IsTransientException", true);
            //                    // Log info about retry attempt (Can't access ILogger in startup.cs)
            //                }));

            // Named HttpClient: Used in controller to re-route email
            services.AddHttpClient("EmailRerouteClient")
                .AddPolicyHandler((service, request) =>
                    HttpPolicyExtensions
                        .HandleTransientHttpError()
                        .Or<TimeoutRejectedException>()
                        .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
						
			// *********************************** POLLY **************************************************

            services.AddSingleton<Publisher, Publisher>();
            services.AddSingleton<Subscriber, Subscriber>();
            services.AddSingleton<IHostedService, MQBackgroundService>();
            services.AddMemoryCache();

            var messageQueueSettings = new MessageQueueSettings();
            Configuration.GetSection("MessageQueueSettings").Bind(messageQueueSettings);
            services.AddSingleton(h => messageQueueSettings);

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Email API",
                    Version = "v1",
                    Description = "Porter Email Web API"
                });

                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Email.Api.xml"));
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PB.Email.xml"));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("v1/swagger.json", "Email.API V1");
            });
        }
    }
}
