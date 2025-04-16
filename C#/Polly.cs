// *********************************** POLLY (HTTPClient Retry policy) **************************************************

using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                                    .SetMinimumLevel(LogLevel.Information)
                                    .AddConsole());
ILogger _logger = loggerFactory.CreateLogger<Program>();

var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(3), retryCount: 3);

// Define policy for handling HTTP transient errors (502, 503, 504, 403, 408, 429)
IAsyncPolicy<HttpResponseMessage> httpWaitAndRetryPolicy =
    Policy<HttpResponseMessage>
        .HandleResult(msg => msg.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.BadGateway)
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
        //.OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.Forbidden)
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(delay, onRetry: (response, waitDuration) =>
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault
                };

                var msg = $"POLLY RETRY POLICY EXECUTED. Response Details: {JsonSerializer.Serialize(response, options: jsonOptions)}";

                _logger.LogError(msg);
            }
            catch (Exception) { }
        });

// Define fallback policy
IAsyncPolicy<HttpResponseMessage> fallbackPolicy =
    Policy.Handle<Exception>().OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .FallbackAsync(
            fallbackAction: (response, context, cancellationToken) =>
            {
                HttpResponseMessage httpResponseMessage = new();
                if (response != null && response.Result != null)
                {
                    httpResponseMessage.StatusCode = response.Result.StatusCode;
                    httpResponseMessage.Content = new StringContent($"Polly fallback executed due to HttpStatusCode {response?.Result?.StatusCode}");

                    return Task.FromResult(httpResponseMessage);
                }

                httpResponseMessage.Content = new StringContent(response.Exception.ToString());

                return Task.FromResult(httpResponseMessage);
            },
            onFallbackAsync: (response, context) =>
            {
                if (response.Exception != null && response.Exception.Data != null)
                    response.Exception.Data.Add("IsTransientException", true);

                try
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        ReferenceHandler = ReferenceHandler.IgnoreCycles,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault
                    };

                    var msg = $"Polly fallback executed." +
                    $"Response: {JsonSerializer.Serialize(response, options: jsonOptions)}" +
                    $"Context: {JsonSerializer.Serialize(context, options: jsonOptions)}";

                    _logger.LogInformation(msg);
                }
                catch (Exception) { }

                return Task.CompletedTask;
            });

IAsyncPolicy<HttpResponseMessage> httpRetryAndFallbackWrapper = Policy.WrapAsync(httpWaitAndRetryPolicy, fallbackPolicy);

builder.Services.AddHttpClient("RetryHttpClient")
    .AddPolicyHandler(httpRetryAndFallbackWrapper);
