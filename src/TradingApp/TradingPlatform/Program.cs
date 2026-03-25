using Infrastructure;
using MatchingEngine;
using Microsoft.Extensions.Options;
using Model.Config;
using Model.Domain;
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

builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IQuoteRepository, QuoteRepository>();
builder.Services.AddSingleton<IProducerQueueSystem<GatewayRequest>, ProducerQueueSystem<GatewayRequest>>(config =>
{
    var gatewayConfig = config.GetRequiredService<IOptions<OrderGatewayConfig>>().Value;
    return new ProducerQueueSystem<GatewayRequest>(gatewayConfig.QueueCapacity);
});

builder.Services.AddHostedService<OrderGatewayService>();
builder.Services.AddHostedService<RiskManagementService>();
builder.Services.AddHostedService<OrderManagementService>();
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
