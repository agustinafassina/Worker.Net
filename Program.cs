using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;
using Worker.SendNotification;
using Worker.SendNotification.Configurations;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;

// Configure HttpClient with retry policy
builder.Services.AddHttpClient<WorkerService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler((services, _) =>
    GetRetryPolicy(services.GetRequiredService<ILogger<WorkerService>>()));

// Configure settings with validation
builder.Services.AddOptions<ExternalApi>()
    .Bind(configuration.GetSection(nameof(ExternalApi)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<HostedSetting>()
    .Bind(configuration.GetSection(nameof(HostedSetting)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("worker", () => HealthCheckResult.Healthy("Worker is running"));

// Register the worker service
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();

// Log startup information
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Worker application starting...");

host.Run();

// Retries only transient failures (network errors, 5xx, 408). Other 4xx responses are not retried.
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger)
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, _) =>
            {
                logger.LogWarning(
                    "Retry {RetryCount} in {Delay}s after {Reason}",
                    retryCount,
                    timespan.TotalSeconds,
                    outcome.Exception?.Message ?? $"status {(int)outcome.Result.StatusCode}");
            });
}
