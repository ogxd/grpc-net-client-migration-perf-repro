using Grpc.Core;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

    public MyClientGrpcNetClient(string host, Action<SocketsHttpHandler> configureHandler = null)
    {
        SocketsHttpHandler handler = new();
        
        GrpcChannelOptions x = new GrpcChannelOptions();
        x.Credentials = ChannelCredentials.Insecure;
        
        if (configureHandler != null)
        {
            x.HttpHandler = handler;
        }
        
        _channel = GrpcChannel.ForAddress("http://" + host, x);
        _client = new PredictionService.PredictionServiceClient(_channel);
        _host = host;
        
        // Configure after GrpcChannel initialized or there is an error ???
        configureHandler?.Invoke(handler);
    }

    public GrpcChannel Channel => _channel;
    
    public async Task<PredictResponse> PredictAsync(PredictRequest request, int timeoutMs)
    {
        try
        {
            //var response = await _client.PredictAsync(request, new CallOptions().WithCancellationToken(new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs)).Token));
            var response = await _client.PredictAsync(request, new CallOptions().WithDeadline(DateTime.UtcNow.AddMilliseconds(timeoutMs)));
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