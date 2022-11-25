using Polly;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Tensorflow.Serving;
using Xunit;
using Xunit.Abstractions;
using GrpcMigrationRepro.Builders;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Latency;

namespace GrpcMigrationRepro.Tests;

public class MyClientTests
{
    private Random _random;
    private readonly ITestOutputHelper _output;

    public MyClientTests(ITestOutputHelper output)
    {
        _random = new Random();
        _output = output;
    }

    [Fact]
    public void Is_Server_GC()
    {
        Assert.True(GCSettings.IsServerGC);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(30)]
    [InlineData(10)]
    public void Test_Grpc_Net_Client(int timeoutMs)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        //using LocalPredictionServer server = new LocalPredictionServer(8500, MonkeyPolicy.InjectLatencyAsync<PredictResponse>(with => with.Latency(TimeSpan.FromSeconds(5)).InjectionRate(0.01).Enabled()));
        using MyClientGrpcNetClient client = new MyClientGrpcNetClient($"127.0.0.1:{server.Port}");

        Test(server, client, 10_000, 50_000, timeoutMs);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(20)]
    [InlineData(10)]
    public void Test_Grpc_Core(int timeoutMs)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        //using LocalPredictionServer server = new LocalPredictionServer(8500, MonkeyPolicy.InjectLatencyAsync<PredictResponse>(with => with.Latency(TimeSpan.FromSeconds(5)).InjectionRate(0.01).Enabled()));
        using MyClientGrpcCore client = new MyClientGrpcCore($"127.0.0.1:{server.Port}");

        Test(server, client, 10_000, 50_000, timeoutMs);
    }

    private void Test(
        LocalPredictionServer server,
        IMyClient client,
        int targetQps,
        int iterations,
        int timeoutMs)
    {
        TimeSpan[] responseTimes = new TimeSpan[iterations];

        TimeSpan targetResponseTime = TimeSpan.FromMilliseconds(1000d / targetQps);

        Stopwatch swTotal = Stopwatch.StartNew();

        // Warmup
        // if (client is MyClientGrpcNetClient c)
        // {
        //     c.Channel.ConnectAsync(CancellationToken.None).Wait();
        //     c.Channel.
        //     client.PredictAsync(CreateRandomRequest(), CancellationToken.None).Wait();
        // }

        Task.WhenAll(Enumerable.Range(0, iterations)
            .Select(async i =>
            {
                try
                {
                    await Task.Delay(i * targetResponseTime);
                    Stopwatch sw = Stopwatch.StartNew();
                    // var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                    var result = await client.PredictAsync(CreateRandomRequest(), timeoutMs);
                    sw.Stop();
                    responseTimes[i] = (result == null) ? TimeSpan.Zero : sw.Elapsed;
                }
                catch (Exception ex)
                {
                    //_output.WriteLine("Error : " + ex);
                }
            }))
            .Wait();

        swTotal.Stop();

        responseTimes = responseTimes.Where(x => x != TimeSpan.Zero).ToArray();

        Array.Sort(responseTimes);

        _output.WriteLine($"Server on’ {server.Port} answered {server.Successes} times");
        _output.WriteLine($"- Average QPS = {Math.Round(iterations / swTotal.Elapsed.TotalSeconds)} calls / s");

        if (responseTimes.Length > 0)
        {
            _output.WriteLine($"- Success rate = {Math.Round(100d * responseTimes.Length / iterations, 2)} %");
            _output.WriteLine($"- Slowest request = {responseTimes[^1].TotalMilliseconds} ms");
            _output.WriteLine($"- 95p quantile = {responseTimes[(int)(0.95d * responseTimes.Length)].TotalMilliseconds} ms");
            _output.WriteLine($"- 50p quantile = {responseTimes[(int)(0.50d * responseTimes.Length)].TotalMilliseconds} ms");
            _output.WriteLine($"- Average = {TimeSpan.FromTicks((long)responseTimes.Average(x => x.Ticks)).TotalMilliseconds} ms");
        }
        else
        {
            _output.WriteLine($"- Success rate = 0 %");
        }
    }

    private PredictRequest CreateRandomRequest()
    {
        return new PredictRequestBuilder()
            .WithModelName("model1")
            .AddInput("input1", i => i
                .WithDimensions(new[] { 1 })
                .WithDtInt32Values(_random.Next()))
            .AddInput("input2", i => i
                .WithDimensions(new[] { 1 })
                .WithDtBoolValues(_random.Next(0, 2) > 0))
            .AddInput("input3", i => i
                .WithDimensions(new[] { 1 })
                .WithDtFloatValues(_random.NextSingle()))
            .AddInput("input4", i => i
                .WithDimensions(new[] { 1 })
                .WithDtDoubleValues(_random.NextDouble()))
            .Build();
    }
}