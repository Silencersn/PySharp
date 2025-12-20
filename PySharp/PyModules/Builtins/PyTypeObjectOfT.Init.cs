using PySharp.PyRuntime;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject<TObject>
{
    private static readonly FrozenDictionary<string, (string PyName, PySpecialMethodParametersType ParamType)> _nameToPySpecialMethodParametersType =
        new Dictionary<string, (string, PySpecialMethodParametersType)>
        {
            [nameof(Repr)] = (PySpecialNames.Repr, PySpecialMethodParametersType.NoArgs),
            [nameof(Str)] = (PySpecialNames.Str, PySpecialMethodParametersType.NoArgs),
            [nameof(Hash)] = (PySpecialNames.Hash, PySpecialMethodParametersType.NoArgs),
            [nameof(Bool)] = (PySpecialNames.Bool, PySpecialMethodParametersType.NoArgs),
            [nameof(Int)] = (PySpecialNames.Int, PySpecialMethodParametersType.NoArgs),
            [nameof(Float)] = (PySpecialNames.Float, PySpecialMethodParametersType.NoArgs),
            [nameof(Complex)] = (PySpecialNames.Complex, PySpecialMethodParametersType.NoArgs),
            [nameof(Index)] = (PySpecialNames.Index, PySpecialMethodParametersType.NoArgs),
            [nameof(Call)] = (PySpecialNames.Call, PySpecialMethodParametersType.ArgsKwargs),
            [nameof(GetAttribute)] = (PySpecialNames.GetAttribute, PySpecialMethodParametersType.String),
            [nameof(GetAttr)] = (PySpecialNames.GetAttr, PySpecialMethodParametersType.String),
            [nameof(SetAttr)] = (PySpecialNames.SetAttr, PySpecialMethodParametersType.StringObject),
            [nameof(DelAttr)] = (PySpecialNames.DelAttr, PySpecialMethodParametersType.String),
            [nameof(Contains)] = (PySpecialNames.Contains, PySpecialMethodParametersType.Object),
            [nameof(GetItem)] = (PySpecialNames.GetItem, PySpecialMethodParametersType.Object),
            [nameof(SetItem)] = (PySpecialNames.SetItem, PySpecialMethodParametersType.ObjectObject),
            [nameof(DelItem)] = (PySpecialNames.DelItem, PySpecialMethodParametersType.Object),
            [nameof(Missing)] = (PySpecialNames.Missing, PySpecialMethodParametersType.Object),
            [nameof(Neg)] = (PySpecialNames.Neg, PySpecialMethodParametersType.NoArgs),
            [nameof(Pos)] = (PySpecialNames.Pos, PySpecialMethodParametersType.NoArgs),
            [nameof(Invert)] = (PySpecialNames.Invert, PySpecialMethodParametersType.NoArgs),
            [nameof(Abs)] = (PySpecialNames.Abs, PySpecialMethodParametersType.NoArgs),
            [nameof(Add)] = (PySpecialNames.Add, PySpecialMethodParametersType.Object),
            [nameof(Sub)] = (PySpecialNames.Sub, PySpecialMethodParametersType.Object),
            [nameof(Mul)] = (PySpecialNames.Mul, PySpecialMethodParametersType.Object),
            [nameof(TrueDiv)] = (PySpecialNames.TrueDiv, PySpecialMethodParametersType.Object),
            [nameof(FloorDiv)] = (PySpecialNames.FloorDiv, PySpecialMethodParametersType.Object),
            [nameof(Mod)] = (PySpecialNames.Mod, PySpecialMethodParametersType.Object),
            [nameof(DivMod)] = (PySpecialNames.DivMod, PySpecialMethodParametersType.Object),
            [nameof(Pow)] = (PySpecialNames.Pow, PySpecialMethodParametersType.ObjectObject),
            [nameof(LShift)] = (PySpecialNames.LShift, PySpecialMethodParametersType.Object),
            [nameof(RShift)] = (PySpecialNames.RShift, PySpecialMethodParametersType.Object),
            [nameof(And)] = (PySpecialNames.And, PySpecialMethodParametersType.Object),
            [nameof(Xor)] = (PySpecialNames.Xor, PySpecialMethodParametersType.Object),
            [nameof(Or)] = (PySpecialNames.Or, PySpecialMethodParametersType.Object),
            [nameof(RAdd)] = (PySpecialNames.RAdd, PySpecialMethodParametersType.Object),
            [nameof(RSub)] = (PySpecialNames.RSub, PySpecialMethodParametersType.Object),
            [nameof(RMul)] = (PySpecialNames.RMul, PySpecialMethodParametersType.Object),
            [nameof(RTrueDiv)] = (PySpecialNames.RTrueDiv, PySpecialMethodParametersType.Object),
            [nameof(RFloorDiv)] = (PySpecialNames.RFloorDiv, PySpecialMethodParametersType.Object),
            [nameof(RMod)] = (PySpecialNames.RMod, PySpecialMethodParametersType.Object),
            [nameof(RDivMod)] = (PySpecialNames.RDivMod, PySpecialMethodParametersType.Object),
            [nameof(RPow)] = (PySpecialNames.RPow, PySpecialMethodParametersType.ObjectObject),
            [nameof(RLShift)] = (PySpecialNames.RLShift, PySpecialMethodParametersType.Object),
            [nameof(RRShift)] = (PySpecialNames.RRShift, PySpecialMethodParametersType.Object),
            [nameof(RAnd)] = (PySpecialNames.RAnd, PySpecialMethodParametersType.Object),
            [nameof(RXor)] = (PySpecialNames.RXor, PySpecialMethodParametersType.Object),
            [nameof(ROr)] = (PySpecialNames.ROr, PySpecialMethodParametersType.Object),
            [nameof(Lt)] = (PySpecialNames.Lt, PySpecialMethodParametersType.Object),
            [nameof(Le)] = (PySpecialNames.Le, PySpecialMethodParametersType.Object),
            [nameof(Eq)] = (PySpecialNames.Eq, PySpecialMethodParametersType.Object),
            [nameof(Ne)] = (PySpecialNames.Ne, PySpecialMethodParametersType.Object),
            [nameof(Gt)] = (PySpecialNames.Gt, PySpecialMethodParametersType.Object),
            [nameof(Ge)] = (PySpecialNames.Ge, PySpecialMethodParametersType.Object),
            [nameof(Get)] = (PySpecialNames.Get, PySpecialMethodParametersType.ObjectObject),
            [nameof(Set)] = (PySpecialNames.Set, PySpecialMethodParametersType.ObjectObject),
            [nameof(Delete)] = (PySpecialNames.Delete, PySpecialMethodParametersType.Object),
            [nameof(Len)] = (PySpecialNames.Len, PySpecialMethodParametersType.NoArgs),
            [nameof(Iter)] = (PySpecialNames.Iter, PySpecialMethodParametersType.NoArgs),
            [nameof(Next)] = (PySpecialNames.Next, PySpecialMethodParametersType.NoArgs),
            [nameof(SetName)] = (PySpecialNames.SetName, PySpecialMethodParametersType.ObjectObject),
            [nameof(Init)] = (PySpecialNames.Init, PySpecialMethodParametersType.ArgsKwargs),
            [nameof(Format)] = (PySpecialNames.Format, PySpecialMethodParametersType.String),
        }.ToFrozenDictionary();

    internal void AppendMethodDescriptors(params ReadOnlySpan<string> names)
    {
        var type = GetType();
        //foreach (var (name, (pyName, paramType)) in _nameToPySpecialMethodParametersType)
        foreach (var name in names)
        {
            var (pyName, paramType) = _nameToPySpecialMethodParametersType[name];

            // TODO: same name
            var method = type
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>));

            Debug.Assert(method is not null);

            PyAttributes.Add(pyName, new PyMethodDescriptorObject2(pyName, this, method, paramType));
        }
    }

    private void AppendOverridenMethodDescriptors()
    {
        var type = GetType();
        foreach (var (name, (pyName, paramType)) in _nameToPySpecialMethodParametersType)
        {
            // //TODO: temp
            //var defaultMethod = typeof(PyTypeObject<TObject>)
            //    .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            //    .Single(method => method.Name == name && method.DeclaringType == typeof(PyTypeObject<TObject>) && !method.IsFinal);
            //Debug.Assert(defaultMethod is not null);

            //var types = defaultMethod.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            //var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, types);

            // TODO: same name
            var method = type
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>));
            
            Debug.Assert(method is not null);
            if (method.DeclaringType == typeof(PyTypeObject<TObject>))
                continue;

            PyAttributes.Add(pyName, new PyMethodDescriptorObject2(pyName, this, method, paramType));
        }
    }
}
