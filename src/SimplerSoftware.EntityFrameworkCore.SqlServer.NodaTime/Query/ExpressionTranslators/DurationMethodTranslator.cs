using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    public class DurationMethodTranslator : BaseNodaTimeMethodCallTranslator
    {
        private static readonly Dictionary<MethodInfo, string> _methodInfoDateAddExtensionMapping = new Dictionary<MethodInfo, string>
        {
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.PlusHours), new[] { typeof(Duration), typeof(double) }), "hour" },
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.PlusMinutes), new[] { typeof(Duration), typeof(double) }), "minute" },
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.PlusSeconds), new[] { typeof(Duration), typeof(double) }), "second" },
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.PlusMilliseconds), new[] { typeof(Duration), typeof(double) }), "millisecond" },
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.PlusMicroseconds), new[] { typeof(Duration), typeof(double) }), "microsecond" },
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.PlusNanoseconds), new[] { typeof(Duration), typeof(double) }), "nanosecond" },
        };

        private static readonly Dictionary<MethodInfo, string> _methodInfoDatePartExtensionMapping = new Dictionary<MethodInfo, string>
        {
            { typeof(DurationExtensions).GetRuntimeMethod(nameof(DurationExtensions.Microseconds), new[] { typeof(Duration) }), "microsecond" },
        };

        private static readonly Dictionary<MethodInfo, string> _methodInfoDateDiffMapping = new Dictionary<MethodInfo, string>
        {
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffHour),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "HOUR"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffHour),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "HOUR"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffMinute),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "MINUTE"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffMinute),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "MINUTE"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffSecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "SECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffSecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "SECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffMillisecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "MILLISECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffMillisecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "MILLISECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffMicrosecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "MICROSECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffMicrosecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "MICROSECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffNanosecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "NANOSECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffNanosecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "NANOSECOND"
            },
        };

        private static readonly Dictionary<MethodInfo, string> _methodInfoDateDiffBigMapping = new Dictionary<MethodInfo, string>
        {
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigSecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "SECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigSecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "SECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigMillisecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "MILLISECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigMillisecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "MILLISECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigMicrosecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "MICROSECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigMicrosecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "MICROSECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigNanosecond),
                    new[] { typeof(DbFunctions), typeof(Duration), typeof(Duration) }),
                "NANOSECOND"
            },
            {
                typeof(SqlServerNodaTimeDbFunctionsExtensions).GetRuntimeMethod(
                    nameof(SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigNanosecond),
                    new[] { typeof(DbFunctions), typeof(Duration?), typeof(Duration?) }),
                "NANOSECOND"
            },
        };

        // The date parts above in nanoseconds, for durations computed as numbers (#38).
        private static readonly Dictionary<string, long> _nanosecondsPerDatePart = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "hour", NodaConstants.NanosecondsPerHour },
            { "minute", NodaConstants.NanosecondsPerMinute },
            { "second", NodaConstants.NanosecondsPerSecond },
            { "millisecond", NodaConstants.NanosecondsPerMillisecond },
            { "microsecond", 1000 },
            { "nanosecond", 1 },
        };

        private static readonly MethodInfo _plus = typeof(Duration).GetRuntimeMethod(nameof(Duration.Plus), new[] { typeof(Duration) });
        private static readonly MethodInfo _minus = typeof(Duration).GetRuntimeMethod(nameof(Duration.Minus), new[] { typeof(Duration) });
        private static readonly MethodInfo _add = typeof(Duration).GetRuntimeMethod(nameof(Duration.Add), new[] { typeof(Duration), typeof(Duration) });
        private static readonly MethodInfo _subtract = typeof(Duration).GetRuntimeMethod(nameof(Duration.Subtract), new[] { typeof(Duration), typeof(Duration) });

        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly DurationSqlTranslator _durations;

        public DurationMethodTranslator(ISqlExpressionFactory sqlExpressionFactory)
            : base(sqlExpressionFactory, null, _methodInfoDateAddExtensionMapping, _methodInfoDatePartExtensionMapping, _methodInfoDateDiffMapping, _methodInfoDateDiffBigMapping)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _durations = new DurationSqlTranslator(sqlExpressionFactory);
        }

        public override SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            // Duration's operators, rewritten into calls by NodaTimeQueryExpressionInterceptor.
            if (DurationOperators.TryGetOperatorType(method, out var operatorType))
            {
                switch (operatorType)
                {
                    case ExpressionType.Add:
                    case ExpressionType.Subtract:
                        return _durations.Arithmetic(operatorType, arguments[0], arguments[1]);
                    case ExpressionType.Negate:
                        return _durations.Negate(arguments[0]);
                    case ExpressionType.Multiply:
                    case ExpressionType.Divide:
                        // Duration / Duration is a double, which integer arithmetic in SQL would get wrong; and NodaTime
                        // rounds a duration times or divided by a double to whole nanoseconds in a way float arithmetic in
                        // SQL wouldn't match. Not translated, so EF Core runs them in .NET where it can.
                        var parameterTypes = method.GetParameters().Select(p => Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType).ToList();
                        if (method.ReturnType == typeof(double) || method.ReturnType == typeof(double?) || parameterTypes.Contains(typeof(double)))
                            return null;
                        return parameterTypes[0] == typeof(long)
                            ? _durations.Scale(operatorType, arguments[1], arguments[0])
                            : _durations.Scale(operatorType, arguments[0], arguments[1]);
                    default:
                        return _durations.Compare(operatorType, arguments[0], arguments[1], logger);
                }
            }

            if (method == DurationOperators.SameStorageMethod || method == DurationOperators.SameNullableStorageMethod)
                return _durations.SameStorage(arguments[0], logger);


            if (method == DurationOperators.ForColumnMethod || method == DurationOperators.ForNullableColumnMethod)
                return _durations.ForColumn(arguments[0], arguments[1]);

            if (method == _plus)
                return _durations.Arithmetic(ExpressionType.Add, instance, arguments[0]);
            if (method == _minus)
                return _durations.Arithmetic(ExpressionType.Subtract, instance, arguments[0]);
            if (method == _add)
                return _durations.Arithmetic(ExpressionType.Add, arguments[0], arguments[1]);
            if (method == _subtract)
                return _durations.Arithmetic(ExpressionType.Subtract, arguments[0], arguments[1]);

            // PlusHours etc. Done in nanoseconds, so the result can pass 24 hours (DATEADD on a time wraps around).
            // A Duration with the user's own converter keeps the DATEADD translation from the base class.
            if (_methodInfoDateAddExtensionMapping.TryGetValue(method, out var plusPart))
                return _durations.Plus(arguments[0], arguments[1], _nanosecondsPerDatePart[plusPart]) ?? base.Translate(instance, method, arguments, logger);

            // Microseconds()
            if (_methodInfoDatePartExtensionMapping.ContainsKey(method) && DurationSqlTranslator.HasArithmeticComponents(DurationSqlTranslator.StorageOf(arguments[0])))
                return _durations.Component(arguments[0], 1000, 1_000_000, method.ReturnType);

            if (_methodInfoDateDiffMapping.TryGetValue(method, out var datePart) || _methodInfoDateDiffBigMapping.TryGetValue(method, out datePart))
            {
                // Time and datetime2 values keep SQL Server's DATEDIFF, which takes them together since it puts a time on
                // 1900-01-01 (a parameter or constant takes the other side's storage, as before). Anything else is counted
                // in nanoseconds the same way; a parameter or constant is then sent as nanoseconds, so it keeps any
                // fraction of a second even next to an int column.
                var storages = new[] { arguments[1], arguments[2] }
                    .Where(a => a.TypeMapping != null)
                    .Select(DurationSqlTranslator.StorageOf)
                    .ToList();
                if (storages.Any(s => s is not (DurationStorage.Time or DurationStorage.DateTime2)) && !storages.Contains(DurationStorage.Other))
                    return _durations.DateDiff(arguments[1], arguments[2], _nanosecondsPerDatePart[datePart], method.ReturnType);
            }

            return base.Translate(instance, method, arguments, logger);
        }

    }
}
