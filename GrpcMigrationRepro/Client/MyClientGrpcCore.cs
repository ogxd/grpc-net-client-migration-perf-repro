using Grpc.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Tensorflow.Serving;

namespace GrpcMigrationRepro;

public sealed class MyClientGrpcCore : IMyClient
{
    private readonly PredictionService.PredictionServiceClient _client;
    private readonly Channel _channel;
    private readonly string _host;

    public string Host => _host;

    public MyClientGrpcCore(string host)
    {
        _channel = new Channel("ipv4:" + host, ChannelCredentials.Insecure);
        _client = new PredictionService.PredictionServiceClient(_channel);
        _host = host;
    }

    public async Task<PredictResponse> PredictAsync(PredictRequest request)
    {
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            var response = await _client.PredictAsync(request);
            sw.Stop();
            response?.SetElapsed(sw.Elapsed);
            return response;
        }
        catch (Exception e)
        {

        }

        return null;
    }

    public void Dispose()
    {
        if (_channel == null)
            return;

        if (_channel.State == ChannelState.Shutdown)
            return;

        try
        {
            _channel.ShutdownAsync().Wait();
        }
        catch (Exception e)
        {
            Trace.WriteLine(e.ToString());
        }
    }
}

