using Grpc.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Tensorflow.Serving;
using Grpc.Net.Client;
using System.Net.Http;

namespace GrpcMigrationRepro;

public sealed class MyClientGrpcNetClient : IMyClient
{
    private readonly PredictionService.PredictionServiceClient _client;
    private readonly GrpcChannel _channel;
    private readonly string _host;

    public string Host => _host;

    public MyClientGrpcNetClient(string host)
    {
        _channel = GrpcChannel.ForAddress("http://" + host, new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Insecure,
            HttpHandler = new SocketsHttpHandler() { EnableMultipleHttp2Connections = true },
        });
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

        if (_channel.State == ConnectivityState.Shutdown)
            return;

        await _channel.ShutdownAsync();
    }
}