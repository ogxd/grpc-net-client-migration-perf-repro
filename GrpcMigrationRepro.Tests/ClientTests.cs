using Polly;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Tensorflow.Serving;
using Xunit;
using Xunit.Abstractions;
using GrpcMigrationRepro.Builders;
using System.Threading;

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
    public async Task Test_Grpc_Net_Client()
    {
        await using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        await using MyClientGrpcNetClient client = new MyClientGrpcNetClient($"127.0.0.1:{server.Port}");

        _output.WriteLine("Warm up:");
        TestReport report = await Test(server, client, 20_000, 100_000);

        Assert.Equal(1, report.successRatio);

        _output.WriteLine("Actual:");
        report = await Test(server, client, 20_000, 100_000);

        Assert.Equal(1, report.successRatio);
    }

    [Fact]
    public async Task Test_Grpc_Core()
    {
        await using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        await using MyClientGrpcCore client = new MyClientGrpcCore($"127.0.0.1:{server.Port}");

        _output.WriteLine("Warm up:");
        TestReport report = await Test(server, client, 20_000, 100_000);

        Assert.Equal(1, report.successRatio);

        _output.WriteLine("Actual:");
        report = await Test(server, client, 20_000, 100_000);

        Assert.Equal(1, report.successRatio);
    }

    private async Task<TestReport> Test(
        LocalPredictionServer server,
        IMyClient client,
        int targetQps,
        int iterations)
    {
        TimeSpan[] responseTimes = new TimeSpan[iterations];

        TimeSpan targetResponseTime = TimeSpan.FromMilliseconds(1000d / targetQps);

        Stopwatch swTotal = Stopwatch.StartNew();

        int maxParallelCalls = 0;
        int currentParallelCalls = 0;

        await Task.WhenAll(Enumerable.Range(0, iterations)
            .Select(async i =>
            {
                try
                {
                    await Task.Delay(i * targetResponseTime);

                    Interlocked.Increment(ref currentParallelCalls);
                    if (currentParallelCalls > maxParallelCalls)
                    {
                        maxParallelCalls = currentParallelCalls;
                    }

                    Stopwatch sw = Stopwatch.StartNew();
                    var result = await client.PredictAsync(CreateRandomRequest());
                    sw.Stop();
                    Interlocked.Decrement(ref currentParallelCalls);

                    responseTimes[i] = (result == null) ? TimeSpan.Zero : sw.Elapsed;
                }
                catch (Exception ex)
                {
                    _output.WriteLine("Error : " + ex);
                }
            }));

        swTotal.Stop();

        responseTimes = responseTimes.Where(x => x != TimeSpan.Zero).ToArray();

        Array.Sort(responseTimes);

        TestReport report = new TestReport();

        if (responseTimes.Length > 0)
        {
            report.quantile95p = responseTimes[(int)(0.95d * responseTimes.Length)];
            report.quantile50p = responseTimes[(int)(0.50d * responseTimes.Length)];
            report.average = TimeSpan.FromTicks((long)responseTimes.Average(x => x.Ticks));
        }
        report.successRatio = 1d * responseTimes.Length / iterations;

        _output.WriteLine($"Server on {server.Port} answered {server.Successes} times");
        _output.WriteLine($"- Average QPS = {Math.Round(iterations / swTotal.Elapsed.TotalSeconds)} calls / s");

        _output.WriteLine($"- Success rate = {Math.Round(100d * report.successRatio, 2)} %");
        _output.WriteLine($"- 95p quantile = {report.quantile95p.TotalMilliseconds} ms");
        _output.WriteLine($"- 50p quantile = {report.quantile50p.TotalMilliseconds} ms");
        _output.WriteLine($"- Average = {report.average.TotalMilliseconds} ms");
        _output.WriteLine($"- Max parallel calls = {maxParallelCalls}");

        return report;
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

    public struct TestReport
    {
        public TimeSpan quantile95p;
        public TimeSpan quantile50p;
        public TimeSpan average;
        public double successRatio;
    }
}