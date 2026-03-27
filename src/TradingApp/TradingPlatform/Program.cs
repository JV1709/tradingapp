using Infrastructure.Event;
using Infrastructure.Queue;
using MatchingEngine;
using Microsoft.Extensions.Options;
using Model.Config;
using Model.Domain;
using Model.Request;
using OrderGateway;
using OrderManagementSystem;
using Repository;
using RiskManagementSystem;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Information);
});

builder.Services.Configure<OrderGatewayConfig>(builder.Configuration.GetSection(nameof(OrderGatewayConfig)));
builder.Services.Configure<MarketConfig>(builder.Configuration.GetSection(nameof(MarketConfig)));
builder.Services.Configure<ParallelismConfig>(builder.Configuration.GetSection(nameof(ParallelismConfig)));

builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IQuoteRepository, QuoteRepository>();
builder.Services.AddSingleton<IEventBus, EventBus>();
builder.Services.AddKeyedSingleton<IPartitionedMPSCQueueSystem<GatewayRequest>>("AccountShardQueue1", (sp, key) =>
{
    var config = sp.GetRequiredService<IOptions<ParallelismConfig>>().Value;
    return new PartitionedMPSCQueueSystem<GatewayRequest>(Enumerable.Range(0, config.PartitionCount).Select(x => x.ToString()));
});
builder.Services.AddKeyedSingleton<IPartitionedSPSCQueueSystem<Order>>("AccountShardQueue2", (sp, key) =>
{
    var config = sp.GetRequiredService<IOptions<ParallelismConfig>>().Value;
    return new PartitionedSPSCQueueSystem<Order>(Enumerable.Range(0, config.PartitionCount).Select(x => x.ToString()));
});
builder.Services.AddKeyedSingleton<IPartitionedMPSCQueueSystem<GatewayRequest>>("InstrumentQueue", (sp, key) =>
{
    var config = sp.GetRequiredService<IOptions<MarketConfig>>().Value;
    return new PartitionedMPSCQueueSystem<GatewayRequest>(config.Instruments.Select(i => i.Symbol));
});
builder.Services.AddSingleton<IRiskManagementConsumerFactory, RiskManagementConsumerFactory>();
builder.Services.AddSingleton<IOrderQueueConsumerFactory, OrderQueueConsumerFactory>();

builder.Services.AddHostedService<OrderGatewayService>();
builder.Services.AddHostedService<OrderManagementService>();
builder.Services.AddHostedService<RiskManagementService>();
builder.Services.AddHostedService<MatchingEngineService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
