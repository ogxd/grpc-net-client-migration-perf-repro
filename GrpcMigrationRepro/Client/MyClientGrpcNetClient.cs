using Grpc.Core;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
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
        GrpcChannelOptions x = new GrpcChannelOptions();
        x.Credentials = ChannelCredentials.Insecure;
        x.HttpHandler = CreateHandler();
        
        _channel = GrpcChannel.ForAddress("http://" + host, x);
        _client = new PredictionService.PredictionServiceClient(_channel);
        _host = host;
    }

    private HttpMessageHandler CreateHandler()
    {
        return new SocketsHttpHandler()
        {
            ConnectCallback = async (ctx, ct) =>
            {
                var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 3600);
                    s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                    s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
                    await s.ConnectAsync(ctx.DnsEndPoint, ct);
                    return new NetworkStream(s, ownsSocket: true);
                }
                catch
                {
                    s.Dispose();
                    throw;
                }
            }
        };
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