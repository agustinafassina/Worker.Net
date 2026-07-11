using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Worker.SendNotification.Configurations;
using Worker.SendNotification.Tests.TestHelpers;

namespace Worker.SendNotification.Tests;

public class WorkerServiceTests
{
    private static readonly ExternalApi ValidExternalApi = new()
    {
        BaseAddress = "https://api.example.com",
        Path = "notify"
    };

    private static readonly HostedSetting ValidHostedSetting = new()
    {
        DelayTimeToStart = 0,
        TimeBetweenExecution = 60_000,
        DailyTime = string.Empty
    };

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(() =>
            new WorkerService(
                null!,
                new HttpClient(new FakeHttpMessageHandler()),
                Options.Create(ValidHostedSetting),
                Options.Create(ValidExternalApi)));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
    {
        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(() =>
            new WorkerService(
                NullLogger<WorkerService>.Instance,
                null!,
                Options.Create(ValidHostedSetting),
                Options.Create(ValidExternalApi)));

        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHostedSettingsIsNull()
    {
        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(() =>
            new WorkerService(
                NullLogger<WorkerService>.Instance,
                new HttpClient(new FakeHttpMessageHandler()),
                null!,
                Options.Create(ValidExternalApi)));

        Assert.Equal("hostedSettings", exception.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenExternalApiIsNull()
    {
        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(() =>
            new WorkerService(
                NullLogger<WorkerService>.Instance,
                new HttpClient(new FakeHttpMessageHandler()),
                Options.Create(ValidHostedSetting),
                null!));

        Assert.Equal("externalApi", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsInvalidOperationException_WhenBaseAddressIsMissing(string? baseAddress)
    {
        var externalApi = new ExternalApi
        {
            BaseAddress = baseAddress!,
            Path = "notify"
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWorkerService(new FakeHttpMessageHandler(), externalApi: externalApi));

        Assert.Equal("ExternalApi.BaseAddress is required", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsInvalidOperationException_WhenPathIsMissing(string? path)
    {
        var externalApi = new ExternalApi
        {
            BaseAddress = "https://api.example.com",
            Path = path!
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWorkerService(new FakeHttpMessageHandler(), externalApi: externalApi));

        Assert.Equal("ExternalApi.Path is required", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenDelayTimeToStartIsNegative()
    {
        var hostedSetting = new HostedSetting
        {
            DelayTimeToStart = -1,
            TimeBetweenExecution = 60_000
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWorkerService(new FakeHttpMessageHandler(), hostedSetting: hostedSetting));

        Assert.Equal("HostedSetting.DelayTimeToStart must be non-negative", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsInvalidOperationException_WhenTimeBetweenExecutionIsNotPositive(int timeBetweenExecution)
    {
        var hostedSetting = new HostedSetting
        {
            DelayTimeToStart = 0,
            TimeBetweenExecution = timeBetweenExecution
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWorkerService(new FakeHttpMessageHandler(), hostedSetting: hostedSetting));

        Assert.Equal("HostedSetting.TimeBetweenExecution must be positive", exception.Message);
    }

    [Fact]
    public void Constructor_LogsWarning_WhenDailyTimeIsInvalid()
    {
        var logger = new TestLogger<WorkerService>();
        var hostedSetting = new HostedSetting
        {
            DelayTimeToStart = 0,
            TimeBetweenExecution = 60_000,
            DailyTime = "invalid-time"
        };

        WorkerService worker = CreateWorkerService(new FakeHttpMessageHandler(), hostedSetting: hostedSetting, logger: logger);

        Assert.NotNull(worker);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.Message.Contains("HostedSetting.DailyTime"));
    }

    [Fact]
    public async Task ExecuteAsync_MakesGetRequestToConfiguredUrl()
    {
        var handler = new FakeHttpMessageHandler();
        var externalApi = new ExternalApi
        {
            BaseAddress = "https://api.example.com/",
            Path = "/notify"
        };
        TestableWorkerService? worker = CreateTestableWorker(handler, externalApi: externalApi);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await worker.ExecuteWorkerAsync(cts.Token);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://api.example.com/notify", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_HandlesSuccessfulResponse()
    {
        var logger = new TestLogger<WorkerService>();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "success-response");
        TestableWorkerService? worker = CreateTestableWorker(handler, logger: logger);

        using CancellationTokenSource? cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await worker.ExecuteWorkerAsync(cts.Token);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.Message.Contains("API request successful"));
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNonSuccessStatusCode()
    {
        var logger = new TestLogger<WorkerService>();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "error");
        TestableWorkerService? worker = CreateTestableWorker(handler, logger: logger);

        using CancellationTokenSource? cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await worker.ExecuteWorkerAsync(cts.Token);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning &&
                     entry.Message.Contains("API request returned non-success status"));
    }

    [Fact]
    public async Task ExecuteAsync_HandlesHttpRequestException()
    {
        var logger = new TestLogger<WorkerService>();
        var handler = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Connection failed"));
        TestableWorkerService worker = CreateTestableWorker(handler, logger: logger);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await worker.ExecuteWorkerAsync(cts.Token);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.Message.Contains("HTTP request failed"));
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefullyWhenCancelled()
    {
        var logger = new TestLogger<WorkerService>();
        var handler = new FakeHttpMessageHandler();
        TestableWorkerService worker = CreateTestableWorker(handler, logger: logger);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.ExecuteWorkerAsync(cts.Token);

        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.Message.Contains("Worker is stopping"));
    }

    [Fact]
    public async Task ExecuteAsync_WaitsInitialDelayBeforeFirstRequest()
    {
        var handler = new FakeHttpMessageHandler();
        var hostedSetting = new HostedSetting
        {
            DelayTimeToStart = 1,
            TimeBetweenExecution = 60_000
        };
        TestableWorkerService? worker = CreateTestableWorker(handler, hostedSetting: hostedSetting);

        using var cts = new CancellationTokenSource();
        var executionTask = worker.ExecuteWorkerAsync(cts.Token);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.Empty(handler.Requests);

        cts.CancelAfter(TimeSpan.FromSeconds(2));
        await executionTask;

        Assert.NotEmpty(handler.Requests);
    }

    private static WorkerService CreateWorkerService(
        FakeHttpMessageHandler handler,
        HostedSetting? hostedSetting = null,
        ExternalApi? externalApi = null,
        ILogger<WorkerService>? logger = null)
    {
        return new WorkerService(
            logger ?? NullLogger<WorkerService>.Instance,
            new HttpClient(handler),
            Options.Create(hostedSetting ?? ValidHostedSetting),
            Options.Create(externalApi ?? ValidExternalApi));
    }

    private static TestableWorkerService CreateTestableWorker(
        FakeHttpMessageHandler handler,
        HostedSetting? hostedSetting = null,
        ExternalApi? externalApi = null,
        ILogger<WorkerService>? logger = null)
    {
        return new TestableWorkerService(
            logger ?? NullLogger<WorkerService>.Instance,
            new HttpClient(handler),
            Options.Create(hostedSetting ?? ValidHostedSetting),
            Options.Create(externalApi ?? ValidExternalApi));
    }

    private sealed class TestableWorkerService : WorkerService
    {
        public TestableWorkerService(
            ILogger<WorkerService> logger,
            HttpClient httpClient,
            IOptions<HostedSetting> hostedSettings,
            IOptions<ExternalApi> externalApi)
            : base(logger, httpClient, hostedSettings, externalApi)
        {
        }

        public Task ExecuteWorkerAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }
}
