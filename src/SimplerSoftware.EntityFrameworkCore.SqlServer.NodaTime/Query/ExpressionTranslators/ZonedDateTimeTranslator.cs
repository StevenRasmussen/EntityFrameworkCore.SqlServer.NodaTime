using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// Stand-ins for comparing an OffsetDateTime's instant with an <see cref="Instant"/>: offsetDateTime.ToInstant() &gt; instant,
    /// including a ZonedDateTime's, which NodaTimeQueryExpressionInterceptor rewrites into these (#22).
    /// <see cref="ZonedDateTimeTranslator"/> compares the datetimeoffset itself, which SQL Server does by instant, so an
    /// index on it can be used. Each does what the comparison does, in case EF Core runs it in .NET.
    /// </summary>
    internal static class InstantComparisons
    {
        public static bool Equal(OffsetDateTime offsetDateTime, Instant instant) => offsetDateTime.ToInstant() == instant;
        public static bool Equal(OffsetDateTime? offsetDateTime, Instant? instant) => offsetDateTime?.ToInstant() == instant;
        public static bool NotEqual(OffsetDateTime offsetDateTime, Instant instant) => offsetDateTime.ToInstant() != instant;
        public static bool NotEqual(OffsetDateTime? offsetDateTime, Instant? instant) => offsetDateTime?.ToInstant() != instant;
        public static bool LessThan(OffsetDateTime offsetDateTime, Instant instant) => offsetDateTime.ToInstant() < instant;
        public static bool LessThan(OffsetDateTime? offsetDateTime, Instant? instant) => offsetDateTime?.ToInstant() < instant;
        public static bool LessThanOrEqual(OffsetDateTime offsetDateTime, Instant instant) => offsetDateTime.ToInstant() <= instant;
        public static bool LessThanOrEqual(OffsetDateTime? offsetDateTime, Instant? instant) => offsetDateTime?.ToInstant() <= instant;
        public static bool GreaterThan(OffsetDateTime offsetDateTime, Instant instant) => offsetDateTime.ToInstant() > instant;
        public static bool GreaterThan(OffsetDateTime? offsetDateTime, Instant? instant) => offsetDateTime?.ToInstant() > instant;
        public static bool GreaterThanOrEqual(OffsetDateTime offsetDateTime, Instant instant) => offsetDateTime.ToInstant() >= instant;
        public static bool GreaterThanOrEqual(OffsetDateTime? offsetDateTime, Instant? instant) => offsetDateTime?.ToInstant() >= instant;

        private static readonly Dictionary<MethodInfo, ExpressionType> _operatorTypes = typeof(InstantComparisons)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(bool) && m.GetParameters().Length == 2 && Enum.TryParse<ExpressionType>(m.Name, out _))
            .ToDictionary(m => m, m => Enum.Parse<ExpressionType>(m.Name));

        private static readonly Dictionary<(ExpressionType OperatorType, bool Nullable), MethodInfo> _standIns = _operatorTypes
            .ToDictionary(p => (p.Value, p.Key.GetParameters()[0].ParameterType == typeof(OffsetDateTime?)), p => p.Key);

        /// <summary>The stand-in for offsetDateTime.ToInstant() OP instant.</summary>
        public static MethodInfo Find(ExpressionType operatorType, bool nullable)
            => _standIns.GetValueOrDefault((operatorType, nullable));

        public static bool TryGetOperatorType(MethodInfo method, out ExpressionType operatorType)
            => _operatorTypes.TryGetValue(method, out operatorType);

        /// <summary>The same comparison with the sides swapped: instant &lt; x becomes x &gt; instant.</summary>
        public static ExpressionType Mirror(ExpressionType operatorType) => operatorType switch
        {
            ExpressionType.LessThan => ExpressionType.GreaterThan,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
            ExpressionType.GreaterThan => ExpressionType.LessThan,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
            _ => operatorType,
        };
    }

    /// <summary>
    /// The translations a <see cref="ZonedDateTime"/> needs once NodaTimeQueryExpressionInterceptor has rewritten its parts in
    /// terms of the two fields it is mapped as (#22): its OffsetDateTime (a datetimeoffset) and its zone. They are useful
    /// for OffsetDateTime and DateTimeZone properties by themselves too.
    /// </summary>
    internal sealed class ZonedDateTimeTranslator : IMethodCallTranslator, IMemberTranslator
    {
        private static readonly MethodInfo _toInstant = typeof(OffsetDateTime).GetMethod(nameof(OffsetDateTime.ToInstant), Type.EmptyTypes);

        // SWITCHOFFSET(datetimeoffset, '+00:00') is NULL when the datetimeoffset is.
        private static readonly bool[] _switchOffsetNullability = { true, false };

        // A char or nchar column pads the ID with spaces, which a zone's ID read back mustn't have. Comparisons ignore them.
        private static readonly ValueConverter<string, string> _trimEnd = new(id => id, id => id.TrimEnd(' '));

        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly IRelationalTypeMappingSource _typeMappingSource;

        public ZonedDateTimeTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _typeMappingSource = typeMappingSource;
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            // offsetDateTime.ToInstant(): the same moment as a UTC datetime2, which is how an Instant is stored.
            if (method == _toInstant)
            {
                var utc = _sqlExpressionFactory.Function(
                    "SWITCHOFFSET",
                    new SqlExpression[] { instance, new SqlFragmentExpression("'+00:00'") },
                    nullable: true,
                    argumentsPropagateNullability: _switchOffsetNullability,
                    typeof(OffsetDateTime),
                    instance.TypeMapping);
                return _sqlExpressionFactory.Convert(utc, typeof(Instant));
            }

            // offsetDateTime.ToInstant() compared with an instant: the datetimeoffset itself, which SQL Server compares by
            // instant, against the instant as stored (a UTC datetime2, read as +00:00), so an index can be used.
            if (InstantComparisons.TryGetOperatorType(method, out var operatorType))
            {
                var offsetDateTime = arguments[0];
                var instant = arguments[1].TypeMapping == null ? _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[1]) : arguments[1];
                return _sqlExpressionFactory.MakeBinary(operatorType, offsetDateTime, instant, typeMapping: null);
            }

            return null;
        }

        public SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (member.DeclaringType == typeof(OffsetDateTime))
            {
                switch (member.Name)
                {
                    // datetimeoffset to datetime2 keeps the local date and time.
                    case nameof(OffsetDateTime.LocalDateTime):
                        return _sqlExpressionFactory.Convert(instance, typeof(LocalDateTime));

                    // DATEPART(tzoffset, ...) is in minutes; an Offset is stored in seconds.
                    case nameof(OffsetDateTime.Offset):
                        var minutes = _sqlExpressionFactory.ApplyDefaultTypeMapping(_sqlExpressionFactory.Function(
                            "DATEPART",
                            new SqlExpression[] { new SqlFragmentExpression("tzoffset"), instance },
                            nullable: true,
                            argumentsPropagateNullability: SqlServerFunctionNullability.DatePart,
                            typeof(int)));
                        return new SqlBinaryExpression(
                            ExpressionType.Multiply,
                            minutes,
                            _sqlExpressionFactory.Constant(60, minutes.TypeMapping),
                            typeof(Offset),
                            OffsetTypeMapping.Default);
                }
            }

            // zone.Id: the stored ID itself, as a string of the column's own type (varchar(32), say), so that a string it
            // is compared with is sent as that type too, and the column needs no conversion (which would rule out an index).
            if (typeof(DateTimeZone).IsAssignableFrom(member.DeclaringType) && member.Name == nameof(DateTimeZone.Id) && instance.TypeMapping is DateTimeZoneTypeMapping zoneMapping)
            {
                var stringMapping = _typeMappingSource.FindMapping(typeof(string), zoneMapping.StoreType)
                    ?? _typeMappingSource.FindMapping(typeof(string));
                if (zoneMapping.IsFixedLength)
                    stringMapping = (RelationalTypeMapping)stringMapping.WithComposedConverter(_trimEnd);
                return instance is ColumnExpression column
                    ? new ColumnExpression(column.Name, column.TableAlias, typeof(string), stringMapping, column.IsNullable)
                    : _sqlExpressionFactory.Convert(instance, typeof(string), stringMapping);
            }

            return null;
        }
    }
}
