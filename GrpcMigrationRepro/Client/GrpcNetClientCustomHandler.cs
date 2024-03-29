using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcMigrationRepro;

public static class GrpcNetClientCustomHandler
{
    public static void InterNetwork(SocketsHttpHandler handler)
    {
        int connectionCount = 0;
        IPAddress ip = null;

        handler.EnableMultipleHttp2Connections = true;
        handler.MaxConnectionsPerServer = 100;
        handler.PooledConnectionLifetime = TimeSpan.FromHours(1);
        handler.AutomaticDecompression = DecompressionMethods.None;
        handler.UseProxy = false;
        handler.UseCookies = false;
        handler.InitialHttp2StreamWindowSize = 16777216;
        
        handler.ConnectCallback = async (context, token) =>
        {
            int connectionsCreated = Interlocked.Increment(ref connectionCount);
            Console.WriteLine($"Create connection (total: {connectionsCreated})");
            
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                LingerState = new LingerOption(true, 0),
                NoDelay = true,
            };

            // var task = Task.Run(async () =>
            // {
            //     await Task.Delay(1000);
            //     Console.WriteLine("Close socket");
            //     socket.Shutdown(SocketShutdown.Receive);
            // });
            
            if (ip == null)
            {
                var ipAddresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, token);
                ip = ipAddresses[Random.Shared.Next(0, ipAddresses.Length)];
            }

            try
            {
                //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, 0);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
                
                await socket.ConnectAsync(ip, context.DnsEndPoint.Port, token).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SOCKET ERROR: " + ex);
                socket.Dispose();
                throw;
            }
        };
    }
    
    public static void BypassToken(SocketsHttpHandler handler)
    {
        int connectionCount = 0;
        IPAddress ip = null;
            
        handler.ConnectCallback = async (context, token) =>
        {
            token = CancellationToken.None;
            
            int connectionsCreated = Interlocked.Increment(ref connectionCount);
            Console.WriteLine($"Create connection (total: {connectionsCreated})");
            
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                LingerState = new LingerOption(true, 0),
                NoDelay = true
            };
            
            if (ip == null)
            {
                var ipAddresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, token);
                ip = ipAddresses[Random.Shared.Next(0, ipAddresses.Length)];
            }

            try
            {
                await socket.ConnectAsync(ip, context.DnsEndPoint.Port, token).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SOCKET ERROR: " + ex);
                socket.Dispose();
                throw;
            }
        };
    }
    
    
    public static void SocketOptions(SocketsHttpHandler handler)
    {
        int connectionCount = 0;
        IPAddress ip = null;
            
        handler.ConnectCallback = async (context, token) =>
        {
            int connectionsCreated = Interlocked.Increment(ref connectionCount);
            Console.WriteLine($"Create connection (total: {connectionsCreated})");
            
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                LingerState = new LingerOption(true, 0),
                NoDelay = true
            };
            
            if (ip == null)
            {
                var ipAddresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, token);
                ip = ipAddresses[Random.Shared.Next(0, ipAddresses.Length)];
            }

            try
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
                
                await socket.ConnectAsync(ip, context.DnsEndPoint.Port, token).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SOCKET ERROR: " + ex);
                socket.Dispose();
                throw;
            }
        };
    }
}