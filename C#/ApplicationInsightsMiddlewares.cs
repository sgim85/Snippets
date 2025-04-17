// See StartupOrProgramConfigs.cs on how middlewares are plugged in at startup
using CXP.Utilities;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace CXP.Common.Middleware
{
    /// <summary>
    /// This middleware adds custom HTTP request telemetry to application insights
    /// </summary>
    public class RequestTelemetryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;

        public RequestTelemetryMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _config = config;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var request = context.Request;
                var method = request.Method;

                if ((request.Method == HttpMethods.Post || request.Method == HttpMethods.Put || request.Method == HttpMethods.Patch) && request.Body.CanRead)
                {
                    // Leave stream open so next middleware can read it
                    using var reader = new StreamReader(
                            context.Request.Body,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: 512, leaveOpen: true);

                    var requestBody = await reader.ReadToEndAsync();

                    bool.TryParse(_config["Telemetry:MaskValues"], out bool maskValues);
                    if (maskValues)
                        requestBody = Utils.MaskJsonValues(requestBody);

                    // Reset stream position, so next middleware can read it
                    context.Request.Body.Position = 0;

                    // Write response body to App Insights
                    var requestTelemetry = context.Features.Get<RequestTelemetry>();
                    requestTelemetry?.Properties.Add("RequestBody", requestBody);

                    var headers = context.Request.Headers;
                    if (headers != null)
                    {
                        foreach (var h in headers)
                        {
                            requestTelemetry?.Properties.Add($"Header_{h.Key}", h.Value);
                        }
                    }
                }
            }
            catch(Exception)
            {

            }
            
            // Call next middleware in the pipeline
            await _next(context);
        }
    }

    /// <summary>
    /// This middleware adds custom HTTP response telemetry to application insights
    /// </summary>
    public class ResponseTelemetryMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;

        public ResponseTelemetryMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _config = config;
        }

        public async Task Invoke(HttpContext context)
        {
            var bodyStream = context.Response.Body;

            try
            {
                // Swap out stream with one that is buffered and suports seeking
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                // hand over to the next middleware and wait for the call to return
                await _next(context);

                // Read response body from memory stream
                memoryStream.Position = 0;
                var reader = new StreamReader(memoryStream);
                var responseBody = await reader.ReadToEndAsync();

                // To reduce telemtry response data size, we need to trim very large values. 
                responseBody = Utils.TrimLargeJsonPropertyValue(responseBody);

                bool.TryParse(_config["Telemetry:MaskValues"], out bool maskValues);
                if (maskValues)
                    responseBody = Utils.MaskJsonValues(responseBody);

                // Copy body back to so its available to the user agent
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(bodyStream);

                // Write response body to App Insights
                var requestTelemetry = context.Features.Get<RequestTelemetry>();
                requestTelemetry?.Properties.Add("ResponseBody", responseBody);
            }
            finally
            {
                context.Response.Body = bodyStream;
            }
        }
    }

    /// <summary>
    /// This class adds custom telemetry for http request dependencies. E.g. api 1 calls api 2, which calls api 3. 2 and 3 are dependencies.
    /// Src: https://learn.microsoft.com/en-us/azure/azure-monitor/app/api-filtering-sampling?tabs=javascriptwebsdkloaderscript#add-properties
    /// </summary>
    public class DependencyTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IConfiguration _config;

        public DependencyTelemetryInitializer(IConfiguration config) 
        { 
            _config = config;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is DependencyTelemetry dependencyTelemetry)
            {
                if (dependencyTelemetry.Properties != null && dependencyTelemetry.TryGetOperationDetail("HttpRequest", out var requestObject))
                {
                    var request = requestObject as HttpRequestMessage;

                    try
                    {
                        if (request?.Content != null && !dependencyTelemetry.Properties.ContainsKey("RequestBody"))
                        {
                            var result = request?.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                            if (result != null)
                            {
                                bool.TryParse(_config["Telemetry:MaskValues"], out bool maskValues);
                                if (maskValues)
                                    result = Utils.MaskJsonValues(result);
                                dependencyTelemetry.Properties.Add("RequestBody", result);
                            }
                        }

                        // Probably not necessary to log headers. Uncomment if need to see dependency headers.
                        var headers = request?.Headers;
                        if (headers != null)
                        {
                            foreach (var h in headers)
                            {
                                if (!string.IsNullOrWhiteSpace(h.Key) && h.Value != null && h.Value.Any())
                                {
                                    var key = $"Header_{h.Key}";

                                    if (!dependencyTelemetry.Properties.ContainsKey(key))
                                        dependencyTelemetry.Properties.Add(key, string.Join(", ", h.Value));
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //....
                    }
                }

                if (dependencyTelemetry.Properties != null && dependencyTelemetry.TryGetOperationDetail("HttpResponse", out var responseObject))
                {
                    var response = responseObject as HttpResponseMessage;

                    try
                    {
                        if (response?.Content != null && !dependencyTelemetry.Properties.ContainsKey("ResponseBody"))
                        {
                            var result = response?.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                            if (result != null)
                            {
                                result = Utils.MaskJsonValues(result);
                                dependencyTelemetry.Properties.Add("ResponseBody", result);
                            }
                        }

                        // Probably not necessary to log headers. Uncomment if need to see dependency headers.
                        var headers = response?.Headers;
                        if (headers != null)
                        {
                            foreach (var h in headers)
                            {
                                if (!string.IsNullOrWhiteSpace(h.Key) && h.Value != null && h.Value.Any())
                                {
                                    var key = $"Header_{h.Key}";

                                    if (!dependencyTelemetry.Properties.ContainsKey(key))
                                        dependencyTelemetry.Properties.Add(key, string.Join(", ", h.Value));
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //....
                    }
                }
            }
        }
    }

    public class CustomPropertiesTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IConfiguration _config;

        public void Initialize(ITelemetry telemetry)
        {
            // Specify Cloud Role Name
            telemetry.Context.Cloud.RoleName = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_ROLE_NAME");
        }
    }
}
