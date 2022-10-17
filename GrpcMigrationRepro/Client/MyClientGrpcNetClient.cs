using Grpc.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Tensorflow.Serving;
using Grpc.Net.Client;

namespace GrpcMigrationRepro;

public sealed class MyClientGrpcNetClient : IMyClient
{
    private readonly PredictionService.PredictionServiceClient _client;
    private readonly GrpcChannel _channel;
    private readonly string _host;

    public string Host => _host;

    public MyClientGrpcNetClient(string host)
    {
        _channel = GrpcChannel.ForAddress("http://" + host, new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure });
        _client = new PredictionService.PredictionServiceClient(_channel);
        _host = host;
    }

    public async Task<PredictResponse> PredictAsync(PredictRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.PredictAsync(request, new CallOptions().WithCancellationToken(cancellationToken));
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

        if (_channel.State == ConnectivityState.Shutdown)
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