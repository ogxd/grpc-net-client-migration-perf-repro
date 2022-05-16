using System;
using Tensorflow;
using Tensorflow.Serving;

namespace GrpcMigrationRepro.Builders;

/// <summary>
/// TensorflowServing request builder
/// </summary>
public class PredictRequestBuilder
{
    /// <summary>
    /// Request being built
    /// </summary>
    public readonly PredictRequest PredictRequest;
    private readonly ModelSpec _modelSpec;

    /// <summary>
    /// Constructs new instance of PredictRequestBuilder
    /// </summary>
    public PredictRequestBuilder()
    {
        _modelSpec = new ModelSpec();
        PredictRequest = new PredictRequest()
        {
            ModelSpec = _modelSpec,
        };
    }

    /// <summary>
    /// Configure and add a new input
    /// </summary>
    public PredictRequestBuilder AddInput(string name, Action<InputBuilder> configure)
    {
        var inputBuilder = new InputBuilder();
        configure(inputBuilder);
        var input = inputBuilder.Build();
        PredictRequest.Inputs.Add(name, input);
        return this;
    }

    /// <summary>
    /// Add a new input
    /// </summary>
    public PredictRequestBuilder AddInput(string name, TensorProto tensorProto)
    {
        PredictRequest.Inputs.Add(name, tensorProto);
        return this;
    }

    /// <summary>
    /// Specifies model name to use in request
    /// </summary>
    public PredictRequestBuilder WithModelName(string modelName)
    {
        _modelSpec.Name = modelName;
        return this;
    }

    /// <summary>
    /// Specifies model version label to use in request
    /// </summary>
    public PredictRequestBuilder WithModelVersionLabel(string modelVersionLabel)
    {
        _modelSpec.VersionLabel = modelVersionLabel;
        return this;
    }

    public PredictRequest Build()
    {
        return PredictRequest;
    }
}