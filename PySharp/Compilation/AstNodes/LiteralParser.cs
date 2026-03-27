using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Compilation.AstNodes;

internal static class LiteralParser
{
    public static PyObject LiteralEval(ReadOnlySpan<char> literal)
    {
        literal = literal.Trim();
        if (literal.IsEmpty)
            throw new NotSupportedException(literal.ToString());

        if (literal is "None")
            return PyNoneObject.None;

        if (literal is "True" or "False")
            return PyBoolObject.FromBoolean(literal is "True");

        if (literal[0] is '"' or '\'')
            return PyStrObject.FromLiteral(literal);

        if (char.IsAsciiDigit(literal[0]) || literal[0] is '.' or '-' or '+')
        {
            if (BigIntegerHelper.TryParse(literal, 0, out var resultInt))
                return PyIntObject.FromInteger(resultInt);

            if (double.TryParse(literal, out var resultDouble))
                return new PyFloatObject(resultDouble);
        }

        if (literal[0] is '[' && literal[^1] is ']')
        {
            if (literal[1..^1].IsWhiteSpace())
                return PyListObject.CreateList();
        }

        if (literal[0] is '(' && literal[^1] is ')')
        {
            if (literal[1..^1].IsWhiteSpace())
                return PyTupleObject.Empty;
        }

        if (literal[0] is '{' && literal[^1] is '}')
        {
            if (literal[1..^1].IsWhiteSpace())
                return PyDictObject.CreateDict();
        }

        if (literal.StartsWith("set") && literal[^1] is ')')
        {
            var span = literal[3..^1];
            span = span.Trim();
            if (span.Length is 1 && span[0] is '(')
                return PySetObject.CreateSet();
        }

        var source = new CodeSource("<literal_eval>", literal.ToString());
        var tokens = Lexer.Tokenize(PyCallContext.NonContextDependency, source);
        var node = Parser.ParseExpression(PyCallContext.NonContextDependency, source, tokens);
        if (TryConvertLiteral(node.Body, out var value))
            return value;
        throw new NotSupportedException(literal.ToString());
    }

    private static bool TryConvertLiteral(AstExprNode node, [NotNullWhen(true)] out PyObject? value)
    {
        value = null;

        List<PyObject> elements;
        switch (node)
        {
            case ConstantNode n:
                value = n.Value;
                return true;

            case CallNode n:
                if (n.Func is not NameNode { Id: string str })
                    return false;
                if (str is not "set")
                    return false;
                if (n.Args.Length > 0 || n.Keywords.Length > 0)
                    return false;
                value = PySetObject.CreateSet();
                return true;

            case UnaryOpNode n:
                if (!TryConvertLiteral(n.Operand, out var operand))
                    return false;

                var unaryResult = PyCore.EvalOperator(PyCallContext.NonContextDependency, n.Op, operand);
                if (unaryResult.IsError)
                    return false;

                value = unaryResult.Value;
                return true;

            case BinOpNode n:
                if (!TryConvertLiteral(n.Left, out var leftOperand))
                    return false;

                if (!TryConvertLiteral(n.Right, out var rightOperand))
                    return false;

                var binResult = PyCore.EvalOperator(PyCallContext.NonContextDependency, n.Operator, leftOperand, rightOperand);
                if (binResult.IsError)
                    return false;

                value = binResult.Value;
                return true;

            case ListNode n:
                if (!TryConvertLiterals(n.Elts, out elements))
                    return false;

                value = PyListObject.CreateList(elements);
                return true;

            case TupleNode n:
                if (!TryConvertLiterals(n.Elts, out elements))
                    return false;

                value = PyTupleObject.CreateTuple(elements);
                return true;

            case SetNode n:
                if (!TryConvertLiterals(n.Elts, out elements))
                    return false;

                value = PySetObject.CreateSet(elements);
                return true;

            case DictNode n:
                foreach (var key in n.Keys)
                {
                    if (key is null)
                        return false;
                }

                if (!TryConvertLiterals(n.Keys!, out var keys))
                    return false;

                if (!TryConvertLiterals(n.Values, out var values))
                    return false;

                if (keys.Count != values.Count)
                    return false;

                value = PyDictObject.CreateDict(keys.Zip(values).Select(static tuple => KeyValuePair.Create(tuple.First, tuple.Second)));
                return true;

            default:
                return false;
        }

        static bool TryConvertLiterals(ImmutableArray<AstExprNode> nodes, out List<PyObject> values)
        {
            values = new List<PyObject>(nodes.Length);
            foreach (var node in nodes)
            {
                if (!TryConvertLiteral(node, out var value))
                    return false;

                values.Add(value);
            }
            return true;
        }
    }
}
