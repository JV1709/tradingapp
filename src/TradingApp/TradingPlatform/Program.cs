using TradingPlatform;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<GatewayWorker>();

var host = builder.Build();
host.Run();
