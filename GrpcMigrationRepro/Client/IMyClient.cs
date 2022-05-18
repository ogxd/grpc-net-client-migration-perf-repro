using System;
using System.Threading.Tasks;
using Tensorflow.Serving;

namespace GrpcMigrationRepro;

public interface IMyClient : IAsyncDisposable
{
    Task<PredictResponse> PredictAsync(PredictRequest request);
}
