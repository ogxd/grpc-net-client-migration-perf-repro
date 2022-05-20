using System;

namespace Tensorflow.Serving;

public partial class PredictResponse
{
    private TimeSpan _elapsed;
    public void SetElapsed(TimeSpan elapsed) => _elapsed = elapsed;
    public TimeSpan GetElapsed() => _elapsed;
}