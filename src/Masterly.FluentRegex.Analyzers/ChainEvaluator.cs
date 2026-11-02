using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Masterly.FluentRegex.Analyzers
{
    /// <summary>The expression uses something that is not a compile-time constant.</summary>
    internal sealed class NotEvaluableException : Exception
    {
        public NotEvaluableException(string message, IOperation operation) : base(message) => Operation = operation;

        public IOperation Operation { get; }
    }

    /// <summary>The library itself threw while building the pattern.</summary>
    internal sealed class PatternConstructionException : Exception
    {
        public PatternConstructionException(Exception inner, IOperation operation) : base(inner.Message, inner) => Operation = operation;

        public IOperation Operation { get; }
    }

    /// <summary>
    /// Evaluates fluent pattern expressions at compile time by interpreting the operation tree and
    /// invoking the real builder code, which is compiled into this analyzer assembly.
    /// </summary>
    internal sealed class ChainEvaluator
    {
        public const string LibraryNamespace = "Masterly.FluentRegex";

        private static readonly Assembly LibraryAssembly = typeof(Pattern).Assembly;
        private static readonly string[] EvaluableTypes = { "Pattern", "Repeat", "CommonPatterns" };

        private readonly Compilation _compilation;
        private readonly CancellationToken _cancellationToken;
        private readonly HashSet<ISymbol> _visiting = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        public ChainEvaluator(Compilation compilation, CancellationToken cancellationToken)
        {
            _compilation = compilation;
            _cancellationToken = cancellationToken;
        }

        public static bool IsPatternType(ITypeSymbol type) =>
            type != null && type.Name == "Pattern" && type.ContainingNamespace?.ToDisplayString() == LibraryNamespace;

        public static bool IsLibraryType(ITypeSymbol type, string name) =>
            type != null && type.Name == name && type.ContainingNamespace?.ToDisplayString() == LibraryNamespace;

        public Pattern EvaluatePattern(IOperation operation)
        {
            object value = Evaluate(operation);
            return value as Pattern ?? throw new NotEvaluableException("the expression does not produce a Pattern", operation);
        }

        public object Evaluate(IOperation operation)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (operation.ConstantValue.HasValue)
                return ConvertConstant(operation.ConstantValue.Value, operation.Type, operation);

            switch (operation)
            {
                case IConversionOperation conversion:
                    return ConvertValue(Evaluate(conversion.Operand), conversion.Type, operation);

                case IParenthesizedOperation parenthesized:
                    return Evaluate(parenthesized.Operand);

                case IPropertyReferenceOperation property:
                    return EvaluateProperty(property);

                case IFieldReferenceOperation field:
                    return EvaluateMemberBody(field.Field, operation);

                case IInvocationOperation invocation:
                    return EvaluateInvocation(invocation);

                case IObjectCreationOperation creation when IsPatternType(creation.Type) && creation.Arguments.Length == 0:
                    return new Pattern();

                case IBinaryOperation binary when binary.OperatorMethod != null && IsPatternType(binary.OperatorMethod.ContainingType):
                    return Invoke(binary.OperatorMethod, null, new[] { Evaluate(binary.LeftOperand), Evaluate(binary.RightOperand) }, operation);

                case IArrayCreationOperation array:
                    return EvaluateArray(array);

                case IDefaultValueOperation defaultValue:
                    return defaultValue.Type != null && defaultValue.Type.IsValueType ? Activator.CreateInstance(RuntimeType(defaultValue.Type, operation)) : null;

                case ILocalReferenceOperation local:
                    throw new NotEvaluableException("local variable '" + local.Local.Name + "' is not a compile-time constant", operation);

                case IParameterReferenceOperation parameter:
                    throw new NotEvaluableException("parameter '" + parameter.Parameter.Name + "' is not a compile-time constant", operation);

                default:
                    throw new NotEvaluableException("'" + operation.Syntax + "' is not a compile-time constant", operation);
            }
        }

        private object EvaluateProperty(IPropertyReferenceOperation operation)
        {
            IPropertySymbol property = operation.Property;
            if (IsEvaluableLibraryType(property.ContainingType))
            {
                object instance = property.IsStatic ? null : Evaluate(operation.Instance);
                PropertyInfo runtime = RuntimeTypeOf(property.ContainingType, operation).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                if (runtime == null) throw new NotEvaluableException("'" + property.Name + "' cannot be evaluated at compile time", operation);
                try
                {
                    return runtime.GetValue(instance);
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    throw new PatternConstructionException(ex.InnerException, operation);
                }
            }

            return EvaluateMemberBody(property, operation);
        }

        private object EvaluateInvocation(IInvocationOperation operation)
        {
            IMethodSymbol method = operation.TargetMethod;

            if (method.ContainingType?.ToDisplayString() == "System.TimeSpan" && method.IsStatic)
                return EvaluateTimeSpan(method, operation);

            if (!IsEvaluableLibraryType(method.ContainingType))
            {
                if (method.IsStatic && method.Parameters.Length == 0) return EvaluateMemberBody(method, operation);
                throw new NotEvaluableException("method '" + method.Name + "' is not part of the fluent API", operation);
            }

            object instance = method.IsStatic ? null : Evaluate(operation.Instance);
            var arguments = new object[method.Parameters.Length];
            foreach (IArgumentOperation argument in operation.Arguments)
                arguments[argument.Parameter.Ordinal] = Evaluate(argument.Value);
            return Invoke(method, instance, arguments, operation);
        }

        private object Invoke(IMethodSymbol method, object instance, object[] arguments, IOperation operation)
        {
            Type type = RuntimeTypeOf(method.ContainingType, operation);
            Type[] parameterTypes = method.Parameters.Select(p => RuntimeType(p.Type, operation)).ToArray();
            MethodInfo runtime = type.GetMethod(method.Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance, null, parameterTypes, null);
            if (runtime == null) throw new NotEvaluableException("'" + method.Name + "' cannot be evaluated at compile time", operation);

            for (int i = 0; i < arguments.Length; i++) arguments[i] = ConvertValue(arguments[i], method.Parameters[i].Type, operation);
            try
            {
                return runtime.Invoke(instance, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new PatternConstructionException(ex.InnerException, operation);
            }
        }

        private object EvaluateTimeSpan(IMethodSymbol method, IInvocationOperation operation)
        {
            // .NET 9 added overloads such as FromMilliseconds(long milliseconds, long microseconds = 0),
            // so each argument is weighted by its parameter name rather than assuming one argument.
            double ticks = 0;
            foreach (IArgumentOperation argument in operation.Arguments)
            {
                double value = Convert.ToDouble(Evaluate(argument.Value), CultureInfo.InvariantCulture);
                string unit = argument.Parameter.Name == "value" ? method.Name.Substring(4).ToLowerInvariant() : argument.Parameter.Name;
                ticks += value * TicksPer(unit, operation);
            }
            return TimeSpan.FromTicks((long)Math.Round(ticks));
        }

        private static double TicksPer(string unit, IOperation operation)
        {
            switch (unit)
            {
                case "days": return TimeSpan.TicksPerDay;
                case "hours": return TimeSpan.TicksPerHour;
                case "minutes": return TimeSpan.TicksPerMinute;
                case "seconds": return TimeSpan.TicksPerSecond;
                case "milliseconds": return TimeSpan.TicksPerMillisecond;
                case "microseconds": return TimeSpan.TicksPerMillisecond / 1000.0;
                case "ticks": return 1;
                default: throw new NotEvaluableException("TimeSpan factory parameter '" + unit + "' is not supported", operation);
            }
        }

        /// <summary>Follows a user-defined static member to its initializer or expression body.</summary>
        private object EvaluateMemberBody(ISymbol symbol, IOperation operation)
        {
            if (!symbol.IsStatic)
                throw new NotEvaluableException("instance member '" + symbol.Name + "' is not a compile-time constant", operation);
            if (symbol.DeclaringSyntaxReferences.Length == 0)
                throw new NotEvaluableException("'" + operation.Syntax + "' is not a compile-time constant", operation);
            if (!_visiting.Add(symbol))
                throw new NotEvaluableException("'" + symbol.Name + "' refers to itself", operation);

            try
            {
                foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
                {
                    ExpressionSyntax expression = BodyExpression(reference.GetSyntax(_cancellationToken));
                    if (expression == null) continue;
                    SemanticModel model = _compilation.GetSemanticModel(expression.SyntaxTree);
                    IOperation body = model.GetOperation(expression, _cancellationToken);
                    if (body != null) return Evaluate(body);
                }
                throw new NotEvaluableException("'" + symbol.Name + "' has no single-expression initializer or body", operation);
            }
            finally
            {
                _visiting.Remove(symbol);
            }
        }

        public static ExpressionSyntax BodyExpression(SyntaxNode declaration)
        {
            switch (declaration)
            {
                case VariableDeclaratorSyntax variable:
                    return variable.Initializer?.Value;
                case PropertyDeclarationSyntax property:
                    if (property.ExpressionBody != null) return property.ExpressionBody.Expression;
                    if (property.Initializer != null) return property.Initializer.Value;
                    AccessorDeclarationSyntax getter = property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration));
                    if (getter?.ExpressionBody != null) return getter.ExpressionBody.Expression;
                    if (getter?.Body?.Statements.Count == 1 && getter.Body.Statements[0] is ReturnStatementSyntax getterReturn) return getterReturn.Expression;
                    return null;
                case MethodDeclarationSyntax method:
                    if (method.ExpressionBody != null) return method.ExpressionBody.Expression;
                    if (method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax methodReturn) return methodReturn.Expression;
                    return null;
                default:
                    return null;
            }
        }

        private object EvaluateArray(IArrayCreationOperation array)
        {
            var arrayType = (IArrayTypeSymbol)array.Type;
            Type elementType = RuntimeType(arrayType.ElementType, array);
            IReadOnlyList<IOperation> elements = array.Initializer?.ElementValues ?? (IReadOnlyList<IOperation>)Array.Empty<IOperation>();
            Array result = Array.CreateInstance(elementType, elements.Count);
            for (int i = 0; i < elements.Count; i++) result.SetValue(ConvertValue(Evaluate(elements[i]), arrayType.ElementType, array), i);
            return result;
        }

        // ------------------------------------------------------------------
        // Types and values
        // ------------------------------------------------------------------

        private static bool IsEvaluableLibraryType(INamedTypeSymbol type) =>
            type != null && type.ContainingNamespace?.ToDisplayString() == LibraryNamespace && Array.IndexOf(EvaluableTypes, type.Name) >= 0;

        private static Type RuntimeTypeOf(INamedTypeSymbol type, IOperation operation) => RuntimeType(type, operation);

        public static Type RuntimeType(ITypeSymbol type, IOperation operation)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_String: return typeof(string);
                case SpecialType.System_Char: return typeof(char);
                case SpecialType.System_Int32: return typeof(int);
                case SpecialType.System_Int64: return typeof(long);
                case SpecialType.System_Boolean: return typeof(bool);
                case SpecialType.System_Double: return typeof(double);
                case SpecialType.System_Object: return typeof(object);
            }

            if (type is IArrayTypeSymbol array) return RuntimeType(array.ElementType, operation).MakeArrayType();

            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return typeof(Nullable<>).MakeGenericType(RuntimeType(named.TypeArguments[0], operation));

            string name = type.ContainingNamespace?.ToDisplayString() + "." + type.MetadataName;
            Type runtime = LibraryAssembly.GetType(name) ?? typeof(Regex).Assembly.GetType(name) ?? typeof(object).Assembly.GetType(name) ?? Type.GetType(name);
            if (runtime == null) throw new NotEvaluableException("type '" + type.ToDisplayString() + "' cannot be evaluated at compile time", operation);
            return runtime;
        }

        private static object ConvertConstant(object value, ITypeSymbol type, IOperation operation)
        {
            if (value == null || type == null) return value;
            if (type.TypeKind == TypeKind.Enum) return Enum.ToObject(RuntimeType(type, operation), value);
            return ConvertValue(value, type, operation);
        }

        private static object ConvertValue(object value, ITypeSymbol type, IOperation operation)
        {
            if (value == null || type == null) return value;
            if (type.TypeKind == TypeKind.Enum)
                return value.GetType().IsEnum && value.GetType() == RuntimeType(type, operation) ? value : Enum.ToObject(RuntimeType(type, operation), value);

            switch (type.SpecialType)
            {
                case SpecialType.System_Int32: return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                case SpecialType.System_Int64: return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case SpecialType.System_Double: return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                case SpecialType.System_Char: return value is char ? value : Convert.ToChar(value, CultureInfo.InvariantCulture);
                default: return value;
            }
        }
    }
}
