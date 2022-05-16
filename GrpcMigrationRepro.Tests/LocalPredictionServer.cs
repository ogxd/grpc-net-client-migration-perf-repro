using Grpc.Core;
using Polly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tensorflow.Serving;

namespace GrpcMigrationRepro.Tests;

public class LocalPredictionServer : IDisposable
{
    private readonly Server _server;
    private readonly int _port;
    private int _successes;

    public int Port => _port;

    public int Successes => _successes;

    public LocalPredictionServer(int port, IAsyncPolicy<PredictResponse> chaosPolicy)
    {
        _port = port;
        _server = new Server
        {
            Services = { PredictionService.BindService(new LocalPredictionService(this, chaosPolicy)) },
            Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
        };
        _server.Start();
    }

    public void Dispose()
    {
        _server.ShutdownAsync().Wait();
    }

    internal class LocalPredictionService : PredictionService.PredictionServiceBase
    {
        private readonly IAsyncPolicy<PredictResponse> _chaosPolicy;
        private readonly LocalPredictionServer _server;

        internal LocalPredictionService(LocalPredictionServer server, IAsyncPolicy<PredictResponse> chaosPolicy)
        {
            _server = server;
            _chaosPolicy = chaosPolicy;
        }

        public override async Task<PredictResponse> Predict(PredictRequest request, ServerCallContext context)
        {
            return await _chaosPolicy.ExecuteAsync(() => PredictInternal(request, context));
        }

        internal async Task<PredictResponse> PredictInternal(PredictRequest request, ServerCallContext context)
        {
            var result = await Task.FromResult(new PredictResponse());
            Interlocked.Increment(ref _server._successes);
            return result;
        }
    }
}