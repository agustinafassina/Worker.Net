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
.AddPolicyHandler(GetRetryPolicy());

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

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => !msg.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                // Logging will be handled by the service itself
            });
}
