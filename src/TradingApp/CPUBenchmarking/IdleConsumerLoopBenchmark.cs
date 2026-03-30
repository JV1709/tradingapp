using BenchmarkDotNet.Attributes;
using Infrastructure.Queue;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Domain;
using Model.Request;
using Microsoft.VSDiagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MatchingEngine.Benchmarks;
[CPUUsageDiagnoser]
public class IdleConsumerLoopBenchmark
{
    private SystemAggregatorConsumer _consumer = null!;
    [GlobalSetup]
    public void GlobalSetup()
    {
        var cancelQueue = new MPSCQueue<CancelOrderRequest>(1024);
        var orderQueue = new MPSCQueue<Order>(1024);
        var commandQueue = new MPSCQueue<MatchingEngineCommand>(1024);
        _consumer = new SystemAggregatorConsumer("BTCUSD", cancelQueue, orderQueue, commandQueue, NullLogger<SystemAggregatorConsumer>.Instance);
    }

    [Benchmark]
    public async Task IdleLoopCpu()
    {
        await _consumer.StartAsync(CancellationToken.None);
        await Task.Delay(25);
        await _consumer.StopAsync(CancellationToken.None);
    }
}