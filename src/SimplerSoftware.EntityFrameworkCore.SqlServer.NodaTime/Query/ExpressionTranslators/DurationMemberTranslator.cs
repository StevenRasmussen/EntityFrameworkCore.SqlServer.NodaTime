using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    public class DurationMemberTranslator : BaseNodaTimeMemberTranslator
    {
        private static readonly Dictionary<string, string> _datePartMapping
            = new Dictionary<string, string>
            {
                { nameof(Duration.Hours), "hour" },
                { nameof(Duration.Minutes), "minute" },
                { nameof(Duration.Seconds), "second" },
                { nameof(Duration.Milliseconds), "millisecond" },
                { nameof(Duration.SubsecondNanoseconds), "nanosecond" },
            };

        private static readonly bool[] _dateDiffPropagatesNullability = { false, false, true };

        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public DurationMemberTranslator([NotNull] ISqlExpressionFactory sqlExpressionFactory)
            : base(sqlExpressionFactory, typeof(Duration), _datePartMapping)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        public override SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            // DATEPART rejects 'day' for a time value, so count the whole days from 1900-01-01 instead: SQL Server
            // treats a time value as being on that date, so this is 0 for a time column (and NULL when it's NULL).
            if (member.DeclaringType == typeof(Duration) && member.Name == nameof(Duration.Days))
            {
                // Only right for a time value. A Duration stored as something else (e.g. through a custom value
                // converter) would give the days since 1900, so leave it untranslated rather than return a wrong value.
                // No type mapping yet means the default Duration mapping, which is time.
                var storeType = instance.TypeMapping?.StoreTypeNameBase?.Trim() ?? SqlServerDateTimeTypes.Time;
                if (!string.Equals(storeType, SqlServerDateTimeTypes.Time, StringComparison.OrdinalIgnoreCase))
                    return null;

                return _sqlExpressionFactory.Function(
                    "DATEDIFF",
                    new SqlExpression[] { new SqlFragmentExpression("day"), new SqlFragmentExpression("'19000101'"), instance },
                    nullable: true,
                    argumentsPropagateNullability: _dateDiffPropagatesNullability,
                    returnType);
            }

            return base.Translate(instance, member, returnType, logger);
        }
    }
}
