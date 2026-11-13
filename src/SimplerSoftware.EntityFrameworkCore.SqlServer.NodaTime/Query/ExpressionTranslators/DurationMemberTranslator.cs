using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using NodaTime;
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

        // Each component as (nanoseconds per unit, modulo), for durations stored as a number (#38).
        private static readonly Dictionary<string, (long NanosecondsPerUnit, long? Modulo)> _components
            = new Dictionary<string, (long, long?)>
            {
                { nameof(Duration.Days), (NodaConstants.NanosecondsPerDay, null) },
                { nameof(Duration.Hours), (NodaConstants.NanosecondsPerHour, 24) },
                { nameof(Duration.Minutes), (NodaConstants.NanosecondsPerMinute, 60) },
                { nameof(Duration.Seconds), (NodaConstants.NanosecondsPerSecond, 60) },
                { nameof(Duration.Milliseconds), (NodaConstants.NanosecondsPerMillisecond, 1000) },
                { nameof(Duration.SubsecondNanoseconds), (1, NodaConstants.NanosecondsPerSecond) },
            };

        private static readonly bool[] _dateDiffPropagatesNullability = { false, false, true };

        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly DurationSqlTranslator _durations;

        public DurationMemberTranslator([NotNull] ISqlExpressionFactory sqlExpressionFactory)
            : base(sqlExpressionFactory, typeof(Duration), _datePartMapping)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _durations = new DurationSqlTranslator(sqlExpressionFactory);
        }

        public override SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (member.DeclaringType != typeof(Duration) || !_components.TryGetValue(member.Name, out var component))
                return base.Translate(instance, member, returnType, logger);

            var storage = DurationSqlTranslator.StorageOf(instance);

            // bigint or int: integer arithmetic, which also handles 24 hours or more and negative durations.
            if (storage is DurationStorage.Nanoseconds or DurationStorage.Seconds)
                return _durations.Component(instance, component.NanosecondsPerUnit, component.Modulo, returnType);

            // DATEPART rejects 'day' for a time value, so count the whole days from 1900-01-01 instead: SQL Server
            // treats a time value as being on that date, so this is 0 for a time column (and NULL when it's NULL).
            if (member.Name == nameof(Duration.Days))
            {
                // Only right for a time value. A Duration stored as something else (e.g. through a custom value
                // converter) would give the days since 1900, so leave it untranslated rather than return a wrong value.
                if (storage != DurationStorage.Time)
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
