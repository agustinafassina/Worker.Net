using Microsoft.Extensions.Configuration;
using Worker.SendNotification;
using Worker.SendNotification.Configurations;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
builder.Services.AddHostedService<WorkerService>();

builder.Services.Configure<ExternalApi>(configuration.GetSection(nameof(ExternalApi)));
builder.Services.Configure<HostedSetting>(configuration.GetSection(nameof(HostedSetting)));

var host = builder.Build();
host.Run();
