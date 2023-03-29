using Grpc.Core;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Equativ.Threading;
using Equativ.Threading.Tasks;
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
    
    public async Task<PredictResponse> PredictAsync2(PredictRequest request, int timeoutMs)
    {
        var pessimisticToken = CoalescingCancellationTokenProvider.Instance.GetCancellationToken(TimeSpan.FromSeconds(60));
        AsyncUnaryCall<PredictResponse> callTask = _client.PredictAsync(request, new CallOptions(cancellationToken: pessimisticToken));
        
        var token = CoalescingCancellationTokenProvider.Instance.GetCancellationToken(TimeSpan.FromMilliseconds(timeoutMs));
        var result = await callTask.ResponseAsync.ToAsyncResult(token);
        
        if (result.IsSuccessful)
        {
            return result.Value;
        }

        switch (result.Exception)
        {
            case OperationCanceledException operationCanceledException:
                // Some code
                break;
            default:
                // Some code
                break;
        }

        return null;
    }
    
    public async Task<PredictResponse> PredictAsync(PredictRequest request, int timeoutMs)
    {
        var token = CoalescingCancellationTokenProvider.Instance.GetCancellationToken(TimeSpan.FromMilliseconds(timeoutMs));
        try
        {
            return await _client.PredictAsync(request, new CallOptions(cancellationToken: token));
        }
        catch (OperationCanceledException operationCanceledException)
        {
            // Some code
        }
        catch
        {
            // Some code
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