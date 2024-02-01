// Program.cs
// *********************************** POLLY **************************************************

var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: 3);

IAsyncPolicy<HttpResponseMessage> httpWaitAndRetryPolicy =
                 HttpPolicyExtensions
                        .HandleTransientHttpError()
                        .Or<TimeoutRejectedException>()
                        .WaitAndRetryAsync(delay, onRetry: (response, waitDuration) => {
                            try
                            {
                                var headers = response?.Result?.Headers;
                                Console.WriteLine($"POLLY RETRY POLICY EXECUTED. StatusCode: {response?.Result?.StatusCode} - {response?.Result?.ReasonPhrase} {Environment.NewLine}Response headers: {(headers != null ? headers.ToString() : string.Empty)}");
                            }
                            catch (Exception) { }
                        });

IAsyncPolicy<HttpResponseMessage> fallbackPolicy =
    Policy.Handle<Exception>().OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .FallbackAsync(
            fallbackAction: (response, context, cancellationToken) =>
            {
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage(response.Result.StatusCode)
                {
                    Content = new StringContent($"Polly fallback executed. Original error was {response?.Result?.ReasonPhrase}")
                };
                return Task.FromResult(httpResponseMessage);
            },
            onFallbackAsync: (response, context) =>
            {
                if (response.Exception != null && response.Exception.Data != null)
                    response.Exception.Data.Add("IsTransientException", true);
                return Task.CompletedTask;
            });

IAsyncPolicy<HttpResponseMessage> httpRetryAndFallbackWrapper = Policy.WrapAsync(fallbackPolicy, httpWaitAndRetryPolicy);

builder.Services.AddHttpClient("RetryHttpClient")
    .AddPolicyHandler(httpRetryAndFallbackWrapper);

// *********************************** POLLY **************************************************
