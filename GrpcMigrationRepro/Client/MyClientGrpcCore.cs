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
        var response = await _client.PredictAsync(request);
        return response;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel == null)
            return;

        if (_channel.State == ChannelState.Shutdown)
            return;

        await _channel.ShutdownAsync();
    }
}