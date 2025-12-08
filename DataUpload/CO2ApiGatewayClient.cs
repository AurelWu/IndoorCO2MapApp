using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.DebugTools;

namespace IndoorCO2MapAppV2.DataUpload
{
        public class Co2ApiGatewayClient
        {
            private static readonly HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            private static readonly Dictionary<SubmissionMode, string> EndpointMap = new()
        {
            { SubmissionMode.Building, "https://wzugdkxj15.execute-api.eu-central-1.amazonaws.com/Standard/CO2" },
            { SubmissionMode.Transit, "https://sokwze8jj1.execute-api.eu-central-1.amazonaws.com/SendTransitCO2DataToSQS" }
        };

            // Define a Polly retry policy
            private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy
                .Handle<HttpRequestException>() // handle network errors
                .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500) // or 5xx server errors
                .WaitAndRetryAsync(
                    // number of retries
                    retryCount: 4,
                    // exponential backoff: wait 2^retryAttempt seconds
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    // on each retry, you can do some logging
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        // outcome.Exception might be null if it's a response-based retry
                        var exception = outcome.Exception;
                        if (exception != null)
                        {
                            Logger.WriteToLog(
                                $"Retry {retryAttempt} due to exception: {exception.Message}. Waiting {timespan}.");
                        }
                        else
                        {
                            Logger.WriteToLog(
                                $"Retry {retryAttempt} due to HTTP {(int)outcome.Result.StatusCode}. Waiting {timespan}.");
                        }
                    }
                );

            public static async Task<Co2ApiResponse> SubmitAsync(string json, SubmissionMode mode)
            {
                if (!EndpointMap.TryGetValue(mode, out var url))
                {
                    return new Co2ApiResponse(false, false, $"Unknown SubmissionMode: {mode}");
                }

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    // Use the policy to execute the HTTP call
                    var response = await RetryPolicy.ExecuteAsync(() =>
                        client.PostAsync(url, content));

                    // Check response
                    if (response.IsSuccessStatusCode)
                    {
                        return new Co2ApiResponse(true, false, null);
                    }
                    else
                    {
                        return new Co2ApiResponse(false, false, $"HTTP {(int)response.StatusCode}");
                    }
                }
                catch (TaskCanceledException e)
                {
                    // Timeout (or cancellation)
                    return new Co2ApiResponse(false, true, $"Timeout: {e.Message}");
                }
                catch (HttpRequestException e)
                {
                    // Network error
                    return new Co2ApiResponse(false, false, $"Network error: {e.Message}");
                }
                catch (Exception e)
                {
                    // Some other error
                    return new Co2ApiResponse(false, false, $"Unexpected error: {e.Message}");
                }
            }
        }
}
