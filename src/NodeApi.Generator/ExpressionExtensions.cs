// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.JavaScript.NodeApi.Generator;

/// <summary>
/// Extension method for generating C# code from expressions.
/// </summary>
internal static class ExpressionExtensions
{
    /// <summary>
    /// Converts a lambda expression to C# code.
    /// </summary>
    /// <remarks>
    /// This supports just enough expression types to handle generating C# code for lambda
    /// expressions constructed by <see cref="Microsoft.JavaScript.NodeApi.DotNetHost.JSMarsaler" />.
    /// </remarks>
    /// <exception cref="NotImplementedException">Thrown if expression includes a node type
    /// for which C# conversion is not implemented.</exception>
    public static string ToCS(this LambdaExpression expression)
        => ToCS(
            expression,
            path: expression.Name ?? string.Empty,
            variables: new HashSet<string>());

    /// <summary>
    /// Recursively traverses an expression tree and builds C# code from the expressions.
    /// </summary>
    /// <param name="expression">The current expression node.</param>
    /// <param name="path">Tracks the path from the root to the current node, for use in error
    /// messages.</param>
    /// <param name="variables">Tracks which variables have been declared; enables declaring
    /// variables on first assignment rather than at the beginning of the block.</param>
    /// <returns>Generated C# code string.</returns>
    private static string ToCS(
        Expression expression,
        string path,
        HashSet<string> variables)
    {
        path += "/" + expression?.NodeType.ToString() ?? string.Empty;

        return expression switch
        {
            null => throw new ArgumentNullException(
                nameof(expression), $"Missing expression at {path}"),

            LambdaExpression lambda =>
                // Format as either a method or a lambda depending on whether this node
                // is inside a block (where variables have been defined).
                (variables.Count == 0 ? FormatType(lambda.ReturnType) + " " + lambda.Name + "(" +
                  string.Join(", ", lambda.Parameters.Select((p) => p.ToCS())) + ")\n" :
                "(" + string.Join(", ", lambda.Parameters.Select((p) => p.ToCS())) + ") =>\n") +
                ToCS(lambda.Body, path, variables),

            ParameterExpression parameter => parameter.Name ?? "_",

            BlockExpression block => FormatBlock(block, path, variables),

            ConstantExpression constant => constant.Type == typeof(Type) ?
                $"typeof({FormatType((Type)constant.Value!)})" : constant.ToString(),

            DefaultExpression defaultExpression => "default",

            UnaryExpression { NodeType: ExpressionType.TypeAs } cast =>
                ToCS(cast.Operand, path, variables) + " as " + FormatType(cast.Type),

            UnaryExpression { NodeType: ExpressionType.Convert } cast =>
                "(" + FormatType(cast.Type) + ")" + ToCS(cast.Operand, path, variables),

            BinaryExpression binary =>
                ToCS(binary.Left, path, variables) +
                binary.NodeType switch
                {
                    ExpressionType.Assign => " = ",
                    ExpressionType.Equal => " == ",
                    ExpressionType.NotEqual => " == ",
                    ExpressionType.Coalesce => " ??\n",
                    _ => throw new NotImplementedException(
                        $"Binary operator not implemented: {binary.NodeType} at {path}")
                } +
                ToCS(binary.Right, path, variables),

            ConditionalExpression conditional =>
                // If type is void then it's an if/then(/else), otherwise it's a ternary expression.
                conditional.Type == typeof(void)
                    ? "if (" + ToCS(conditional.Test, path, variables) +
                      ") { " + ToCS(conditional.IfTrue, path, variables) + "; }" +
                      (conditional.IfFalse is DefaultExpression ? string.Empty :
                      " else { " + ToCS(conditional.IfFalse, path, variables) + "; }")
                    : ToCS(conditional.Test, path, variables) + " ?\n" +
                      ToCS(conditional.IfTrue, path, variables) + " :\n" +
                      ToCS(conditional.IfFalse, path, variables),

            MemberExpression { NodeType: ExpressionType.MemberAccess } member =>
                (member.Expression != null ? WithParentheses(member.Expression, path, variables) :
                    member.Member.DeclaringType!.FullName) + "." + member.Member.Name,

            MethodCallExpression { Method.Name: "op_Explicit" or "op_Implicit" } cast =>
                "(" + FormatType(cast.Method.ReturnType) + ")" +
                WithParentheses(cast.Arguments[0], path, variables),

            MethodCallExpression { Method.Name: "get_Item" } index =>
                WithParentheses(index.Object!, path, variables) +
                "[" + ToCS(index.Arguments[0], path, variables) + "]",

            MethodCallExpression { Method.IsSpecialName: true } call =>
                call.Method.Name.StartsWith("get_") ?
                    (call.Method.IsStatic ?
                        string.Concat(FormatType(call.Method.DeclaringType!),
                            ".", call.Method.Name.AsSpan(4)) :
                        string.Concat(WithParentheses(call.Object!, path, variables),
                            ".", call.Method.Name.AsSpan(4))) :
                call.Method.Name.StartsWith("set_") ?
                    (call.Method.IsStatic ?
                        string.Concat(FormatType(call.Method.DeclaringType!),
                            ".", call.Method.Name.AsSpan(4)) :
                        string.Concat(WithParentheses(call.Object!, path, variables),
                            ".", call.Method.Name.AsSpan(4))) +
                    " = " + ToCS(call.Arguments.Single(), path, variables) :
                throw new NotImplementedException("Special method not implemented: " + call.Method),

            MethodCallExpression call =>
                call.Method.IsStatic && call.Method.IsDefined(typeof(ExtensionAttribute), false)
                    ? WithParentheses(call.Arguments.First(), path, variables) +
                        "." + call.Method.Name +
                        FormatArgs(call.Arguments.Skip(1), path, variables) :
                call.Method.IsStatic
                    ? FormatType(call.Method.DeclaringType!) + "." + call.Method.Name +
                        FormatArgs(call.Method, call.Arguments, path, variables)
                    : WithParentheses(call.Object!, path, variables) + "." + call.Method.Name +
                        FormatArgs(call.Method, call.Arguments, path, variables),

            IndexExpression { Object: not null, Arguments.Count: 1 } index =>
                    ToCS(index.Object, path, variables) +
                    "[" + ToCS(index.Arguments[0], path, variables) + "]",

            InvocationExpression invocation =>
                ((LambdaExpression)invocation.Expression).Name +
                    FormatArgs(invocation.Arguments, path, variables),

            NewExpression construction =>
                "new " + FormatType(construction.Type) +
                    FormatArgs(construction.Arguments, path, variables),

            NewArrayExpression { NodeType: ExpressionType.NewArrayBounds } newArray =>
                "new " + FormatType(newArray.Type.GetElementType()!) +
                "[" + ToCS(newArray.Expressions.Single(), path, variables) + "]",

            NewArrayExpression { NodeType: ExpressionType.NewArrayInit } newArray =>
                "new " + FormatType(newArray.Type.GetElementType()!) + "[] { " +
                string.Join(", ", newArray.Expressions.Select((a) => ToCS(a, path, variables))) +
                " }",

            GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression =>
                "return " + ToCS(gotoExpression.Value!, path, variables),

            LabelExpression label => label.DefaultValue != null ?
                ToCS(label.DefaultValue, path, variables) : "???",

            _ => throw new NotImplementedException(
                "Expression type not implemented: " +
                $"{expression.GetType().Name} ({expression.NodeType}) at {path}"),
        };
    }

    private static string ToCS(this ParameterExpression parameter)
        => $"{FormatType(parameter.Type)} {parameter.Name ?? "_"}";

    private static string WithParentheses(
        Expression expression,
        string path,
        HashSet<string> variables)
    {
        string cs = ToCS(expression, path, variables);

        if (cs.StartsWith("(") &&
            (expression.NodeType == ExpressionType.TypeAs ||
            expression.NodeType == ExpressionType.Convert ||
            expression.NodeType == ExpressionType.Call ||
            expression.NodeType == ExpressionType.MemberAccess))
        {
            // Wrap extra parentheses around casts when needed.
            cs = $"({cs})";
        }

        return cs;
    }

    private static string FormatBlock(
        BlockExpression block,
        string path,
        HashSet<string> variables)
    {
        StringBuilder s = new();
        s.Append("{\n");

        for (int i = 0; i < block.Expressions.Count; i++)
        {
            bool isReturn = i == block.Expressions.Count - 1 && block.Type != typeof(void);
            string statement = FormatStatement(
                block.Expressions[i], isReturn, path, ref variables);
            s.Append(statement + '\n');
        }

        s.Append('}');
        return s.ToString();
    }

    private static string FormatStatement(
        Expression expression, bool isReturn, string path, ref HashSet<string> variables)
    {
        string s = string.Empty;

        if (expression.NodeType == ExpressionType.Assign)
        {
            BinaryExpression assignment = (BinaryExpression)expression;
            if (assignment.Left is ParameterExpression variable &&
                !variables.Contains(variable.Name!))
            {
                variables = new HashSet<string>(variables.Union(new[] { variable.Name! }));
                s += FormatType(variable.Type) + " " + s;
            }
        }

        s += ToCS(expression, path, variables);

        if (!s.EndsWith("}"))
        {
            s += ';';
        }

        if (isReturn)
        {
            s = "return " + s;
        }

        return s;
    }

    internal static string FormatType(Type type)
    {
        if (type.IsGenericType)
        {
            if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return FormatType(type.GetGenericArguments()[0]) + "?";
            }

            string typeArgs = string.Join(", ", type.GenericTypeArguments.Select(FormatType));
            return $"{type.Namespace}.{type.Name.Substring(0, type.Name.IndexOf('`'))}<{typeArgs}>";
        }
        else if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            return FormatType(elementType) + "[]";
        }
        else if (type.IsPrimitive)
        {
            switch (type.Name)
            {
                case nameof(Boolean): return "bool";
                case nameof(SByte): return "sbyte";
                case nameof(Byte): return "byte";
                case nameof(Int16): return "short";
                case nameof(UInt16): return "ushort";
                case nameof(Int32): return "int";
                case nameof(UInt32): return "uint";
                case nameof(Int64): return "long";
                case nameof(UInt64): return "ulong";
                case nameof(IntPtr): return "nint";
                case nameof(UIntPtr): return "nuint";
                case nameof(Single): return "float";
                case nameof(Double): return "double";
            }
        }
        else if (type == typeof(string))
        {
            return "string";
        }
        else if (type == typeof(void))
        {
            return "void";
        }

        return type.FullName ?? "(anonymous)";
    }

    private static string FormatArgs(
        MethodInfo method,
        IReadOnlyCollection<Expression> arguments,
        string path,
        HashSet<string> variables)
        => (method.IsGenericMethod
            ? "<" + string.Join(", ", method.GetGenericArguments().Select(FormatType)) + ">"
            : string.Empty) + FormatArgs(arguments, path, variables);

    private static string FormatArgs(
        IEnumerable<Expression> arguments,
        string path,
        HashSet<string> variables)
        => "(" + string.Join(", ", arguments.Select((a) => ToCS(a, path, variables))) + ")";
}
