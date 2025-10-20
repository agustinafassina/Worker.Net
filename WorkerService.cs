using Microsoft.Extensions.Options;
using Worker.SendNotification.Configurations;

namespace Worker.SendNotification
{
    public class WorkerService : BackgroundService
    {
        private readonly ILogger<WorkerService> _logger;
        private readonly int _delaySeconds;
        private readonly string _apiUrl;
        private readonly string _apiPath;
        private static readonly HttpClient _httpClient = new HttpClient();

        public WorkerService(ILogger<WorkerService> logger, IOptions<HostedSetting> options, IOptions<ExternalApi> optionsApi)
        {
            _logger = logger;
            _delaySeconds = options.Value.DelayTimeToStart;
            _apiUrl = optionsApi.Value.BaseAddress;
            _apiPath = optionsApi.Value.Path;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}. Configured API URL: {apiUrl}", DateTimeOffset.Now, _apiUrl);
                    GetRequestToExternalApi();
                }

                await Task.Delay(TimeSpan.FromSeconds(_delaySeconds), stoppingToken);
            }
        }

        private async void GetRequestToExternalApi()
        {
            string url = _apiUrl + _apiPath;

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Error in the request: {e.Message}");
            }
        }
    }
}