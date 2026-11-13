using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// Translates combining a <see cref="LocalDate"/> and a <see cref="LocalTime"/> into a <see cref="LocalDateTime"/>:
    /// date.At(time), time.On(date), date + time, and date.AtMidnight() (#48).
    /// </summary>
    internal class LocalDateTimeCombineTranslator : IMethodCallTranslator
    {
        private static readonly MethodInfo _localDateAt = typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.At), new[] { typeof(LocalTime) });
        private static readonly MethodInfo _localTimeOn = typeof(LocalTime).GetRuntimeMethod(nameof(LocalTime.On), new[] { typeof(LocalDate) });
        private static readonly MethodInfo _localDateAtMidnight = typeof(LocalDate).GetRuntimeMethod(nameof(LocalDate.AtMidnight), Type.EmptyTypes);

        // date + time; NodaTimeQueryExpressionInterceptor turns the operator into a call to this method.
        internal static readonly MethodInfo LocalDatePlusLocalTime = typeof(LocalDate).GetRuntimeMethod("op_Addition", new[] { typeof(LocalDate), typeof(LocalTime) });


        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public LocalDateTimeCombineTranslator(ISqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (method == _localDateAt)
                return Combine(date: instance, time: arguments[0]);

            if (method == _localTimeOn)
                return Combine(date: arguments[0], time: instance);

            if (method == LocalDatePlusLocalTime)
                return Combine(date: arguments[0], time: arguments[1]);

            if (method == _localDateAtMidnight)
                return _sqlExpressionFactory.Convert(instance, typeof(LocalDateTime));

            return null;
        }

        /// <summary>
        /// DATEADD(day, DATEDIFF(day, '19000101', date), CAST(time AS datetime2)).
        /// Casting a time to datetime2 gives that time on 1900-01-01; adding the number of days from 1900-01-01 to
        /// the date moves it onto the date. Exact to 100ns for all dates, and NULL if either side is NULL.
        /// </summary>
        private SqlExpression Combine(SqlExpression date, SqlExpression time)
        {
            var timeAsDateTime2 = _sqlExpressionFactory.Convert(time, typeof(LocalDateTime));

            var daysSince1900 = _sqlExpressionFactory.Function(
                "DATEDIFF",
                new SqlExpression[] { new SqlFragmentExpression("day"), new SqlFragmentExpression("'19000101'"), date },
                nullable: true,
                argumentsPropagateNullability: SqlServerFunctionNullability.DateDiffFrom1900,
                typeof(int));

            return _sqlExpressionFactory.Function(
                "DATEADD",
                new SqlExpression[] { new SqlFragmentExpression("day"), daysSince1900, timeAsDateTime2 },
                nullable: true,
                argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                typeof(LocalDateTime),
                timeAsDateTime2.TypeMapping);
        }
    }
}
