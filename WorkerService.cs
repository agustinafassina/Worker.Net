using Microsoft.Extensions.Options;
using System.Net;
using Worker.SendNotification.Configurations;

namespace Worker.SendNotification
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly HttpClient _httpClient;
        private readonly HostedSetting _hostedSettings;
        private readonly ExternalApi _externalApi;

        public WorkerService(
            ILogger<WorkerService> logger,
            HttpClient httpClient,
            IOptions<HostedSetting> hostedSettings,
            IOptions<ExternalApi> externalApi)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            
            _hostedSettings = hostedSettings?.Value ?? throw new ArgumentNullException(nameof(hostedSettings));
            _externalApi = externalApi?.Value ?? throw new ArgumentNullException(nameof(externalApi));

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_externalApi.BaseAddress))
                throw new InvalidOperationException("ExternalApi.BaseAddress is required");

            if (string.IsNullOrWhiteSpace(_externalApi.Path))
                throw new InvalidOperationException("ExternalApi.Path is required");

            if (_hostedSettings.DelayTimeToStart < 0)
                throw new InvalidOperationException("HostedSetting.DelayTimeToStart must be non-negative");

            if (_hostedSettings.TimeBetweenExecution <= 0)
                throw new InvalidOperationException("HostedSetting.TimeBetweenExecution must be positive");

            if (!TimeSpan.TryParse(_hostedSettings.DailyTime, out _))
                _logger.LogWarning("HostedSetting.DailyTime '{DailyTime}' is not a valid time format. Will use TimeBetweenExecution instead.", _hostedSettings.DailyTime);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay before starting
            if (_hostedSettings.DelayTimeToStart > 0)
            {
                _logger.LogInformation("Worker starting in {DelaySeconds} seconds...", _hostedSettings.DelayTimeToStart);
                await Task.Delay(TimeSpan.FromSeconds(_hostedSettings.DelayTimeToStart), stoppingToken);
            }

            _logger.LogInformation("Worker started at: {Time}", DateTimeOffset.Now);

            // If DailyTime is configured, use it for scheduling
            bool useDailyTime = TimeSpan.TryParse(_hostedSettings.DailyTime, out var dailyTime);
            if (useDailyTime)
            {
                _logger.LogInformation("Using daily schedule at: {DailyTime}", dailyTime);
            }
            else
            {
                var delaySeconds = _hostedSettings.TimeBetweenExecution / 1000;
                _logger.LogInformation("Using interval schedule: {DelaySeconds} seconds between executions", delaySeconds);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (useDailyTime)
                    {
                        // Wait until the scheduled time
                        var now = DateTime.Now;
                        var targetTime = now.Date.Add(dailyTime);
                        
                        // If the time has passed today, schedule for tomorrow
                        if (targetTime <= now)
                        {
                            targetTime = targetTime.AddDays(1);
                        }

                        var delay = targetTime - now;
                        _logger.LogInformation("Waiting until {TargetTime} (in {Delay})", targetTime, delay);
                        await Task.Delay(delay, stoppingToken);
                    }

                    await GetRequestToExternalApiAsync(stoppingToken);

                    // Wait for the configured time between executions
                    if (!useDailyTime)
                    {
                        // Use TimeBetweenExecution (convert from milliseconds to seconds)
                        var delaySeconds = _hostedSettings.TimeBetweenExecution / 1000;
                        _logger.LogInformation("Waiting {DelaySeconds} seconds until next execution", delaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                    }
                    // If using daily time, the loop will recalculate the next day's time
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Worker is stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred in the worker");
                    // Wait a bit before retrying to avoid tight error loops
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }

            _logger.LogInformation("Worker stopped at: {Time}", DateTimeOffset.Now);
        }


        private async Task GetRequestToExternalApiAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_externalApi.BaseAddress) || string.IsNullOrWhiteSpace(_externalApi.Path))
            {
                _logger.LogError("API configuration is invalid. BaseAddress or Path is missing.");
                return;
            }

            var url = $"{_externalApi.BaseAddress.TrimEnd('/')}/{_externalApi.Path.TrimStart('/')}";
            _logger.LogInformation("Making request to: {Url}", url);

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation(
                        "API request successful. Status: {StatusCode}, Response length: {Length} characters",
                        response.StatusCode,
                        responseBody.Length);
                    
                    // Log a preview of the response (first 500 chars) for debugging
                    if (responseBody.Length > 500)
                    {
                        _logger.LogDebug("Response preview: {Preview}...", responseBody.Substring(0, 500));
                    }
                    else
                    {
                        _logger.LogDebug("Response: {Response}", responseBody);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "API request returned non-success status: {StatusCode} {ReasonPhrase}",
                        response.StatusCode,
                        response.ReasonPhrase);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed: {Message}", ex.Message);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("Request timeout after 30 seconds");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Request was cancelled");
                throw; // Re-throw cancellation to allow graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during API request");
            }
        }
    }
}
