using NodaTime;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// Stand-ins that <see cref="NodaTimeQueryExpressionInterceptor"/> puts into a query, so that
    /// <see cref="DurationMethodTranslator"/> can translate them knowing how each Duration is stored (#38).
    /// EF Core translates operators, ?? and ?: itself, without asking a plugin.
    /// <para>
    /// Each method does exactly what the code it replaces does (the operator, including for nulls, or nothing), in
    /// case EF Core runs it in .NET. ?? and ?: stay inside <see cref="SameStorage(Duration)"/>, so they still only
    /// evaluate the branch they need.
    /// </para>
    /// </summary>
    internal static class DurationOperators
    {
        public static bool Equal(Duration left, Duration right) => left == right;
        public static bool Equal(Duration? left, Duration? right) => left == right;
        public static bool NotEqual(Duration left, Duration right) => left != right;
        public static bool NotEqual(Duration? left, Duration? right) => left != right;
        public static bool LessThan(Duration left, Duration right) => left < right;
        public static bool LessThan(Duration? left, Duration? right) => left < right;
        public static bool LessThanOrEqual(Duration left, Duration right) => left <= right;
        public static bool LessThanOrEqual(Duration? left, Duration? right) => left <= right;
        public static bool GreaterThan(Duration left, Duration right) => left > right;
        public static bool GreaterThan(Duration? left, Duration? right) => left > right;
        public static bool GreaterThanOrEqual(Duration left, Duration right) => left >= right;
        public static bool GreaterThanOrEqual(Duration? left, Duration? right) => left >= right;
        public static Duration Add(Duration left, Duration right) => left + right;
        public static Duration? Add(Duration? left, Duration? right) => left + right;
        public static Duration Subtract(Duration left, Duration right) => left - right;
        public static Duration? Subtract(Duration? left, Duration? right) => left - right;
        public static Duration Multiply(Duration left, long right) => left * right;
        public static Duration? Multiply(Duration? left, long? right) => left * right;
        public static Duration Multiply(long left, Duration right) => left * right;
        public static Duration? Multiply(long? left, Duration? right) => left * right;
        public static Duration Multiply(Duration left, double right) => left * right;
        public static Duration? Multiply(Duration? left, double? right) => left * right;
        public static Duration Multiply(double left, Duration right) => left * right;
        public static Duration? Multiply(double? left, Duration? right) => left * right;
        public static Duration Divide(Duration left, double right) => left / right;
        public static Duration? Divide(Duration? left, double? right) => left / right;
        public static Duration Divide(Duration left, long right) => left / right;
        public static Duration? Divide(Duration? left, long? right) => left / right;
        public static double Divide(Duration left, Duration right) => left / right;
        public static double? Divide(Duration? left, Duration? right) => left / right;
        public static Duration Negate(Duration value) => -value;
        public static Duration? Negate(Duration? value) => -value;

        /// <summary>A ?? or ?: between Durations, whose branches SQL needs in the same storage.</summary>
        public static Duration SameStorage(Duration value) => value;

        /// <summary>A ?? or ?: between Durations, whose branches SQL needs in the same storage.</summary>
        public static Duration? SameStorage(Duration? value) => value;

        /// <summary>A value that ExecuteUpdate writes into a column, which must be in the column's storage.</summary>
        public static Duration ForColumn(Duration value, Duration column) => value;

        /// <summary>A value that ExecuteUpdate writes into a column, which must be in the column's storage.</summary>
        public static Duration? ForColumn(Duration? value, Duration? column) => value;

        private static readonly Dictionary<MethodInfo, ExpressionType> _operatorTypes = new Dictionary<MethodInfo, ExpressionType>();
        private static readonly Dictionary<(MethodInfo NodaTimeOperator, bool Lifted), MethodInfo> _standIns = new Dictionary<(MethodInfo, bool), MethodInfo>();

        static DurationOperators()
        {
            var nodaTimeNames = new Dictionary<ExpressionType, string>
            {
                { ExpressionType.Equal, "op_Equality" },
                { ExpressionType.NotEqual, "op_Inequality" },
                { ExpressionType.LessThan, "op_LessThan" },
                { ExpressionType.LessThanOrEqual, "op_LessThanOrEqual" },
                { ExpressionType.GreaterThan, "op_GreaterThan" },
                { ExpressionType.GreaterThanOrEqual, "op_GreaterThanOrEqual" },
                { ExpressionType.Add, "op_Addition" },
                { ExpressionType.Subtract, "op_Subtraction" },
                { ExpressionType.Multiply, "op_Multiply" },
                { ExpressionType.Divide, "op_Division" },
                { ExpressionType.Negate, "op_UnaryNegation" },
            };

            foreach (var standIn in typeof(DurationOperators).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!System.Enum.TryParse<ExpressionType>(standIn.Name, out var operatorType) || !nodaTimeNames.TryGetValue(operatorType, out var nodaTimeName))
                    continue;

                _operatorTypes.Add(standIn, operatorType);

                // The NodaTime operator with the same parameters, without the nullable wrapping.
                var parameterTypes = standIn.GetParameters().Select(p => System.Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType).ToArray();
                var lifted = standIn.GetParameters().Any(p => System.Nullable.GetUnderlyingType(p.ParameterType) != null);
                var nodaTimeOperator = typeof(Duration).GetMethod(nodaTimeName, BindingFlags.Public | BindingFlags.Static, parameterTypes);
                if (nodaTimeOperator != null)
                    _standIns.Add((nodaTimeOperator, lifted), standIn);
            }
        }

        public static readonly MethodInfo EqualMethod = typeof(DurationOperators).GetMethod(nameof(Equal), new[] { typeof(Duration), typeof(Duration) });
        public static readonly MethodInfo EqualNullableMethod = typeof(DurationOperators).GetMethod(nameof(Equal), new[] { typeof(Duration?), typeof(Duration?) });
        public static readonly MethodInfo SameStorageMethod = typeof(DurationOperators).GetMethod(nameof(SameStorage), new[] { typeof(Duration) });
        public static readonly MethodInfo SameNullableStorageMethod = typeof(DurationOperators).GetMethod(nameof(SameStorage), new[] { typeof(Duration?) });
        public static readonly MethodInfo ForColumnMethod = typeof(DurationOperators).GetMethod(nameof(ForColumn), new[] { typeof(Duration), typeof(Duration) });
        public static readonly MethodInfo ForNullableColumnMethod = typeof(DurationOperators).GetMethod(nameof(ForColumn), new[] { typeof(Duration?), typeof(Duration?) });

        /// <summary>The stand-in for one of Duration's operators, or null if there isn't one.</summary>
        public static MethodInfo StandInFor(MethodInfo nodaTimeOperator, bool lifted)
            => nodaTimeOperator != null && _standIns.TryGetValue((nodaTimeOperator, lifted), out var standIn) ? standIn : null;

        public static bool TryGetOperatorType(MethodInfo method, out ExpressionType operatorType)
            => _operatorTypes.TryGetValue(method, out operatorType);

        public static bool IsComparison(ExpressionType operatorType)
            => operatorType is ExpressionType.Equal or ExpressionType.NotEqual
                or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
                or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual;
    }
}
