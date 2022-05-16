using Google.Protobuf;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tensorflow;
using static Tensorflow.TensorShapeProto.Types;

namespace GrpcMigrationRepro.Builders;

/// <summary>
/// TensorflowServing client input builder
/// </summary>
public class InputBuilder
{
    private readonly TensorShapeProto _tensorShapeProto;
    private readonly TensorProto _tensorProto;

    /// <summary>
    /// Constructs new InputBuilder
    /// </summary>
    public InputBuilder()
    {
        _tensorShapeProto = new TensorShapeProto();
        _tensorProto = new TensorProto()
        {
            TensorShape = _tensorShapeProto
        };
    }

    /// <summary>
    /// Specifies dimensions of input tensor 
    /// </summary>
    public InputBuilder WithDimensions(IEnumerable<int> dimensions)
    {
        foreach (var dimension in dimensions)
        {
            _tensorShapeProto.Dim.Add(new Dim
            {
                Size = dimension
            });
        }
        return this;
    }

    /// <summary>
    /// Adds floating point inputs to builder
    /// </summary>
    public InputBuilder WithDtFloatValues(params float[] values)
    {
        _tensorProto.Dtype = DataType.DtFloat;
        _tensorProto.FloatVal.AddRange(values);
        return this;
    }

    /// <summary>
    /// Adds double floating point inputs to builder
    /// </summary>
    public InputBuilder WithDtDoubleValues(params double[] values)
    {
        _tensorProto.Dtype = DataType.DtDouble;
        _tensorProto.DoubleVal.AddRange(values);
        return this;
    }

    /// <summary>
    /// Adds string inputs to builder
    /// </summary>
    public InputBuilder WithDtStringValues(Encoding encoding, params string[] values)
    {
        _tensorProto.Dtype = DataType.DtString;
        _tensorProto.StringVal.AddRange(values.Select(v => ByteString.CopyFrom(v, encoding)));
        return this;
    }

    /// <summary>
    /// Adds int32 inputs to builder
    /// </summary>
    public InputBuilder WithDtInt32Values(params int[] values)
    {
        _tensorProto.Dtype = DataType.DtInt32;
        _tensorProto.IntVal.AddRange(values);
        return this;
    }

    /// <summary>
    /// Adds boolean inputs to builder
    /// </summary>
    public InputBuilder WithDtBoolValues(params bool[] values)
    {
        _tensorProto.Dtype = DataType.DtBool;
        _tensorProto.BoolVal.AddRange(values);
        return this;
    }

    /// <summary>
    /// Returns tensor proto and name
    /// </summary>
    /// <returns></returns>
    public TensorProto Build()
    {
        return _tensorProto;
    }
}