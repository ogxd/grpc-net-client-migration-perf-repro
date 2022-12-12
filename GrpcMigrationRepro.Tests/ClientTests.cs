using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using Tensorflow.Serving;
using GrpcMigrationRepro.Builders;
using NUnit.Framework;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Latency;

namespace GrpcMigrationRepro.Tests;

public enum ClientLib
{
    GrpcCore,
    GrpcNetClient
}

public class MyClientTests
{
    private readonly Random _random= new();

    [Test]
    public void Is_Server_GC()
    {
        Assert.True(GCSettings.IsServerGC);
    }

    private IMyClient CreateClientLib(ClientLib clientLib, int port) => clientLib switch
    {
        ClientLib.GrpcCore => new MyClientGrpcCore($"127.0.0.1:{port}"),
        ClientLib.GrpcNetClient => new MyClientGrpcNetClient($"127.0.0.1:{port}", GrpcNetClientCustomHandler.BypassToken),
    };

    [Test]
    public void Benchmark_Latency([Values(200)] int timeoutMs, [Values] ClientLib clientLib)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, MonkeyPolicy.InjectLatencyAsync<PredictResponse>(with => with.Latency(TimeSpan.FromSeconds(5)).InjectionRate(0.01).Enabled()));
        using IMyClient client = CreateClientLib(clientLib, server.Port);
        Test(server, client, 5_000, 50_000, timeoutMs);
    }
    
    [Test]
    public void Benchmark_Small_Timeout([Values(20)] int timeoutMs, [Values] ClientLib clientLib)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        using IMyClient client = CreateClientLib(clientLib, server.Port);
        Test(server, client, 5_000, 50_000, timeoutMs);
    }
    
    [Test]
    public void Benchmark_High_QPS([Values(200)] int timeoutMs, [Values] ClientLib clientLib)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        using IMyClient client = CreateClientLib(clientLib, server.Port);
        Test(server, client, 20_000, 100_000, timeoutMs);
    }
    
    [Test]
    public void Benchmark_Ultrahigh_QPS([Values(200)] int timeoutMs, [Values] ClientLib clientLib)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        using IMyClient client = CreateClientLib(clientLib, server.Port);
        Test(server, client, 50_000, 200_000, timeoutMs);
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

        Console.WriteLine($"Server on’ {server.Port} answered {server.Successes} times");
        Console.WriteLine($"- Average QPS = {Math.Round(iterations / swTotal.Elapsed.TotalSeconds)} calls / s");

        if (responseTimes.Length > 0)
        {
            Console.WriteLine($"- Success rate = {Math.Round(100d * responseTimes.Length / iterations, 2)} %");
            Console.WriteLine($"- Slowest request = {responseTimes[^1].TotalMilliseconds} ms");
            Console.WriteLine($"- 99p quantile = {responseTimes[(int)(0.99d * responseTimes.Length)].TotalMilliseconds} ms");
            Console.WriteLine($"- 95p quantile = {responseTimes[(int)(0.95d * responseTimes.Length)].TotalMilliseconds} ms");
            Console.WriteLine($"- 50p quantile = {responseTimes[(int)(0.50d * responseTimes.Length)].TotalMilliseconds} ms");
            Console.WriteLine($"- Average = {TimeSpan.FromTicks((long)responseTimes.Average(x => x.Ticks)).TotalMilliseconds} ms");
        }
        else
        {
            Console.WriteLine($"- Success rate = 0 %");
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