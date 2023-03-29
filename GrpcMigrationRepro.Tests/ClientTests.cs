using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Tensorflow.Serving;
using GrpcMigrationRepro.Builders;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using NUnit.Framework;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Latency;
using Smart.Monitoring;

namespace GrpcMigrationRepro.Tests;

public enum ClientLib
{
    GrpcCore,
    GrpcNetClient
}

public class MyClientTests
{
    private readonly Random _random = new();

    [Test]
    public void Is_Server_GC()
    {
        Assert.True(GCSettings.IsServerGC);
    }

    private IMyClient CreateClientLib(ClientLib clientLib, int port) => clientLib switch
    {
        ClientLib.GrpcCore => new MyClientGrpcCore($"127.0.0.1:{port}"),
        ClientLib.GrpcNetClient => new MyClientGrpcNetClient($"127.0.0.1:{port}"/*, GrpcNetClientCustomHandler.InterNetwork*/),
        //ClientLib.GrpcNetClient => new MyClientGrpcNetClient($"127.0.0.11:{port}"),
    };

    [Test]
    public void Benchmark_Latency([Values(200)] int timeoutMs, [Values] ClientLib clientLib)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, MonkeyPolicy.InjectLatencyAsync<PredictResponse>(with => with.Latency(TimeSpan.FromSeconds(5)).InjectionRate(0.01).Enabled()));
        using IMyClient client = CreateClientLib(clientLib, server.Port);
        Test(server, client, 15_000, 50_000, timeoutMs);
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
        int qps = 20000;
        Test(server, client, qps, 5 * qps, timeoutMs);
    }
    
    [Test]
    public void Benchmark_Ultrahigh_QPS([Values(0)] int timeoutMs, [Values] ClientLib clientLib)
    {
        using LocalPredictionServer server = new LocalPredictionServer(8500, Policy.NoOpAsync<PredictResponse>());
        using IMyClient client = CreateClientLib(clientLib, server.Port);
        Test(server, client, 10_000, 100_000, timeoutMs);
    }

    private void Test(
        LocalPredictionServer server,
        IMyClient client,
        int targetQps,
        int iterations,
        int timeoutMs)
    {
        ExceptionMonitor exceptionMonitor = new();
        //MemoryMonitor memoryMonitor = new();
        
        ThreadPool.QueueUserWorkItem(_ =>
        {
            exceptionMonitor.Start();
        });
        //
        // ThreadPool.QueueUserWorkItem(_ =>
        // {
        //     memoryMonitor.Start();
        // });
       
        
        // ThreadPool.QueueUserWorkItem(_ =>
        // {
        //     Thread.Sleep(5000);
        //     server.Dispose();
        //     Console.WriteLine("Server stopped");
        //     Thread.Sleep(5000);
        //     server.Start();
        //     Console.WriteLine("Server restarted");
        // });
        
        Thread.Sleep(500);

        Result[] results = new Result[iterations];

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
                await Task.Delay(i * targetResponseTime);

                if (30 * i % iterations == 0)
                {
                    //Console.WriteLine($"Exceptions thrown: {exceptionMonitor.ExceptionCount}");
                    //GC.Collect();
                    //var gcinfo = GC.GetGCMemoryInfo(GCKind.FullBlocking);
                    //Console.WriteLine($"Heap Size: {0.001 * 0.001 * gcinfo.HeapSizeBytes}mb");
                    //Console.WriteLine($"Gen0: {0.001 * 0.001 * memoryMonitor.GetGenerationSize(0)}mb, Gen1: {0.001 * 0.001 * memoryMonitor.GetGenerationSize(1)}mb, Gen2: {0.001 * 0.001 * memoryMonitor.GetGenerationSize(2)}mb, Gen3: {0.001 * 0.001 * memoryMonitor.GetGenerationSize(3)}mb");
                }
                
                var status = CallStatus.Null;
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    // var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                    var result = await client.PredictAsync(CreateRandomRequest(), timeoutMs);
                    if (result != null)
                        status = CallStatus.Ok;
                }
                catch (Exception ex)
                {
                    status = CallStatus.Exception;
                }
                sw.Stop();
                results[i] = new Result(sw.Elapsed, status);
            }))
            .Wait();

        swTotal.Stop();

        results = results.OrderBy(x => x.ResponseTime).ToArray();

        Console.WriteLine($"Exceptions thrown: {exceptionMonitor.ExceptionCount}");
        Console.WriteLine($"Server on’ {server.Port} answered {server.Successes} times");
        Console.WriteLine($"- Average QPS = {Math.Round(iterations / swTotal.Elapsed.TotalSeconds)} calls / s");

        Console.WriteLine($"- Success rate = {Math.Round(100d * results.Count(x => x.CallStatus == CallStatus.Ok) / iterations, 4)} %");
        Console.WriteLine($"- Exception rate = {Math.Round(100d * results.Count(x => x.CallStatus == CallStatus.Exception) / iterations, 4)} %");
        Console.WriteLine($"- Null rate = {Math.Round(100d * results.Count(x => x.CallStatus == CallStatus.Null) / iterations, 4)} %");
        Console.WriteLine($"- Slowest request = {results[^1].ResponseTime.TotalMilliseconds} ms");
        Console.WriteLine($"- 99p quantile = {results[(int)(0.99d * results.Length)].ResponseTime.TotalMilliseconds} ms");
        Console.WriteLine($"- 95p quantile = {results[(int)(0.95d * results.Length)].ResponseTime.TotalMilliseconds} ms");
        Console.WriteLine($"- 50p quantile = {results[(int)(0.50d * results.Length)].ResponseTime.TotalMilliseconds} ms");
        Console.WriteLine($"- Average = {TimeSpan.FromTicks((long)results.Select(x => x.ResponseTime).Average(x => x.Ticks)).TotalMilliseconds} ms");
    }

    public record struct Result(TimeSpan ResponseTime, CallStatus CallStatus);
    
    public enum  CallStatus
    {
        Ok,
        Null,
        Exception,
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