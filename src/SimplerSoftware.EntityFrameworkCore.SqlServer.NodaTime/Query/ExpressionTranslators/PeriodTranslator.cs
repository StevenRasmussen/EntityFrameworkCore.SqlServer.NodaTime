using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// Stand-ins for NodaTime code that uses a <see cref="Period"/> (#3). A Period is mapped as a complex type, which SQL
    /// can't pass to a function as a value, so NodaTimeQueryExpressionInterceptor rewrites date + period,
    /// period.ToDuration() and so on into these, which take the period's ten units instead: each one a value EF Core can
    /// translate. <see cref="PeriodTranslator"/> translates them. Each does what the code it stands in for does, in case
    /// EF Core runs it in .NET.
    /// </summary>
    internal static class PeriodStandIns
    {
        /// <summary>value + period, value.Plus(period); and with each unit negated, value - period and value.Minus(period).</summary>
        public static LocalDateTime Plus(LocalDateTime value, int years, int months, int weeks, int days, long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
            => value.Plus(Build(years, months, weeks, days, hours, minutes, seconds, milliseconds, ticks, nanoseconds));

        /// <inheritdoc cref="Plus(LocalDateTime, int, int, int, int, long, long, long, long, long, long)"/>
        public static LocalDate Plus(LocalDate value, int years, int months, int weeks, int days, long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
            => value.Plus(Build(years, months, weeks, days, hours, minutes, seconds, milliseconds, ticks, nanoseconds));

        /// <inheritdoc cref="Plus(LocalDateTime, int, int, int, int, long, long, long, long, long, long)"/>
        public static LocalTime Plus(LocalTime value, int years, int months, int weeks, int days, long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
            => value.Plus(Build(years, months, weeks, days, hours, minutes, seconds, milliseconds, ticks, nanoseconds));

        /// <summary>
        /// value + period, value - period and so on, where the period is a parameter or constant: EF Core can't read its
        /// units in SQL, so it's passed whole, as JSON.
        /// </summary>
        public static LocalDateTime PlusPeriod(LocalDateTime value, Period period, bool negate) => negate ? value.Minus(period) : value.Plus(period);

        /// <inheritdoc cref="PlusPeriod(LocalDateTime, Period, bool)"/>
        public static LocalDate PlusPeriod(LocalDate value, Period period, bool negate) => negate ? value.Minus(period) : value.Plus(period);

        /// <inheritdoc cref="PlusPeriod(LocalDateTime, Period, bool)"/>
        public static LocalTime PlusPeriod(LocalTime value, Period period, bool negate) => negate ? value.Minus(period) : value.Plus(period);


        /// <summary>period.HasDateComponent.</summary>
        public static bool HasDateComponent(int years, int months, int weeks, int days)
            => years != 0 || months != 0 || weeks != 0 || days != 0;

        /// <summary>period.HasTimeComponent.</summary>
        public static bool HasTimeComponent(long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
            => hours != 0 || minutes != 0 || seconds != 0 || milliseconds != 0 || ticks != 0 || nanoseconds != 0;

        /// <summary>period.ToDuration().</summary>
        public static Duration ToDuration(int years, int months, int weeks, int days, long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
            => Build(years, months, weeks, days, hours, minutes, seconds, milliseconds, ticks, nanoseconds).ToDuration();

        /// <summary>Period.Between(start, end, unit), for a single date unit, and that unit of it: its Years for PeriodUnits.Years.</summary>
        public static int DateUnitsBetween(LocalDateTime start, LocalDateTime end, PeriodUnits unit) => (int)UnitOf(Period.Between(start, end, unit), unit);

        /// <inheritdoc cref="DateUnitsBetween(LocalDateTime, LocalDateTime, PeriodUnits)"/>
        public static int DateUnitsBetween(LocalDate start, LocalDate end, PeriodUnits unit) => (int)UnitOf(Period.Between(start, end, unit), unit);

        /// <summary>Period.Between(start, end, unit), for a single time unit, and that unit of it: its Hours for PeriodUnits.Hours.</summary>
        public static long TimeUnitsBetween(LocalDateTime start, LocalDateTime end, PeriodUnits unit) => UnitOf(Period.Between(start, end, unit), unit);

        /// <inheritdoc cref="TimeUnitsBetween(LocalDateTime, LocalDateTime, PeriodUnits)"/>
        public static long TimeUnitsBetween(LocalTime start, LocalTime end, PeriodUnits unit) => UnitOf(Period.Between(start, end, unit), unit);

        private static readonly Type[] _unitTypes = PeriodConvention.Units.Select(u => u.PropertyType).ToArray();
        private static readonly Type[] _valueTypes = { typeof(LocalDateTime), typeof(LocalDate), typeof(LocalTime) };

        public static readonly MethodInfo ToDurationMethod = Method(nameof(ToDuration), _unitTypes);
        public static readonly MethodInfo HasDateComponentMethod = Method(nameof(HasDateComponent), _unitTypes.Take(4));
        public static readonly MethodInfo HasTimeComponentMethod = Method(nameof(HasTimeComponent), _unitTypes.Skip(4));

        private static readonly Dictionary<Type, MethodInfo> _plusMethods
            = _valueTypes.ToDictionary(t => t, t => Method(nameof(Plus), _unitTypes.Prepend(t)));

        private static readonly Dictionary<Type, MethodInfo> _plusPeriodMethods
            = _valueTypes.ToDictionary(t => t, t => Method(nameof(PlusPeriod), new[] { t, typeof(Period), typeof(bool) }));

        private static readonly Dictionary<(Type ValueType, Type UnitType), MethodInfo> _unitsBetweenMethods = typeof(PeriodStandIns)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name is nameof(DateUnitsBetween) or nameof(TimeUnitsBetween))
            .ToDictionary(m => (m.GetParameters()[0].ParameterType, m.ReturnType));

        private static readonly Dictionary<string, PeriodUnits> _units
            = PeriodConvention.Units.ToDictionary(u => u.Name, u => Enum.Parse<PeriodUnits>(u.Name));

        /// <summary>The Plus stand-in for a LocalDateTime, LocalDate or LocalTime; null for any other type.</summary>
        public static MethodInfo PlusMethod(Type valueType) => _plusMethods.GetValueOrDefault(valueType);

        /// <summary>The PlusPeriod stand-in for a LocalDateTime, LocalDate or LocalTime; null for any other type.</summary>
        public static MethodInfo PlusPeriodMethod(Type valueType) => _plusPeriodMethods.GetValueOrDefault(valueType);

        /// <summary>The stand-in for one unit of Period.Between(start, end, unit), returning the unit's type; null if there's none.</summary>
        public static MethodInfo UnitsBetweenMethod(Type valueType, Type unitType) => _unitsBetweenMethods.GetValueOrDefault((valueType, unitType));

        /// <summary>The single unit a Period property is: PeriodUnits.Days for Days. Null for any other member.</summary>
        public static PeriodUnits? UnitOf(string memberName) => _units.TryGetValue(memberName, out var unit) ? unit : null;

        private static MethodInfo Method(string name, IEnumerable<Type> parameterTypes)
            => typeof(PeriodStandIns).GetMethod(name, parameterTypes.ToArray());

        private static Period Build(int years, int months, int weeks, int days, long hours, long minutes, long seconds, long milliseconds, long ticks, long nanoseconds)
            => new PeriodBuilder
            {
                Years = years, Months = months, Weeks = weeks, Days = days, Hours = hours, Minutes = minutes, Seconds = seconds,
                Milliseconds = milliseconds, Ticks = ticks, Nanoseconds = nanoseconds,
            }.Build();

        private static long UnitOf(Period period, PeriodUnits unit) => unit switch
        {
            PeriodUnits.Years => period.Years,
            PeriodUnits.Months => period.Months,
            PeriodUnits.Weeks => period.Weeks,
            PeriodUnits.Days => period.Days,
            PeriodUnits.Hours => period.Hours,
            PeriodUnits.Minutes => period.Minutes,
            PeriodUnits.Seconds => period.Seconds,
            PeriodUnits.Milliseconds => period.Milliseconds,
            PeriodUnits.Ticks => period.Ticks,
            PeriodUnits.Nanoseconds => period.Nanoseconds,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };
    }

    /// <summary>
    /// Translates <see cref="PeriodStandIns"/> (#3), following what NodaTime does:
    /// <list type="bullet">
    /// <item>Adding a period adds its years, then its months (both as DATEADD does, which ends up on the last day of a
    /// shorter month as NodaTime does), then its weeks and days, then its time units as elapsed time. A LocalTime wraps
    /// around midnight. NodaTime refuses time units for a LocalDate, and date units for a LocalTime; so does the SQL.</item>
    /// <item>ToDuration counts a week as 7 days and a day as 24 hours, and refuses years and months, as NodaTime does.</item>
    /// <item>A single unit between two values is the most whole units that can be added to the start without passing the
    /// end: DATEDIFF, less one if adding that many does pass it.</item>
    /// </list>
    /// Time is kept to the 100ns of a datetime2 or time; anything smaller is dropped, as saving a value drops it.
    /// </summary>
    internal sealed class PeriodTranslator : IMethodCallTranslator
    {
        private static readonly MethodInfo _daysBetween = typeof(Period).GetMethod(nameof(Period.DaysBetween), new[] { typeof(LocalDate), typeof(LocalDate) });

        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly IRelationalTypeMappingSource _typeMappingSource;
        private readonly DurationSqlTranslator _durations;
        private readonly RelationalTypeMapping _intTypeMapping;
        private readonly RelationalTypeMapping _longTypeMapping;
        private readonly RelationalTypeMapping _stringTypeMapping;

        public PeriodTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _typeMappingSource = typeMappingSource;
            _durations = new DurationSqlTranslator(sqlExpressionFactory);
            _intTypeMapping = typeMappingSource.FindMapping(typeof(int));
            _longTypeMapping = typeMappingSource.FindMapping(typeof(long));
            _stringTypeMapping = typeMappingSource.FindMapping(typeof(string));
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (method == _daysBetween)
                return DateDiff("day", arguments[0], arguments[1], typeof(int), _intTypeMapping);

            if (method.DeclaringType != typeof(PeriodStandIns))
                return null;

            if (method.Name == nameof(PeriodStandIns.PlusPeriod))
            {
                var value = arguments[0].TypeMapping == null ? _sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0]) : arguments[0];
                return arguments[2] is SqlConstantExpression { Value: bool negate } && UnitsOf(arguments[1], negate) is { } periodUnits
                    ? Plus(method.ReturnType, value, periodUnits)
                    : null;
            }

            var values = arguments.Select(a => a.TypeMapping == null ? _sqlExpressionFactory.ApplyDefaultTypeMapping(a) : a).ToArray();
            switch (method.Name)
            {
                case nameof(PeriodStandIns.Plus):
                    return Plus(method.ReturnType, values[0], values.Skip(1).ToArray());

                case nameof(PeriodStandIns.ToDuration):
                    return ToDuration(values);

                case nameof(PeriodStandIns.HasDateComponent):
                case nameof(PeriodStandIns.HasTimeComponent):
                    return AnyNonZero(values);

                case nameof(PeriodStandIns.DateUnitsBetween):
                case nameof(PeriodStandIns.TimeUnitsBetween):
                    return arguments[2] is SqlConstantExpression { Value: PeriodUnits unit }
                        ? UnitsBetween(values[0], values[1], unit, method.GetParameters()[0].ParameterType)
                        : null;
            }

            return null;
        }

        // Units: 0 years, 1 months, 2 weeks, 3 days, 4 hours, 5 minutes, 6 seconds, 7 milliseconds, 8 ticks, 9 nanoseconds.

        private SqlExpression Plus(Type valueType, SqlExpression value, SqlExpression[] units)
            => valueType == typeof(LocalDateTime) ? PlusToDateTime(value, units)
                : valueType == typeof(LocalDate) ? PlusToDate(value, units)
                : PlusToTime(value, units);

        /// <summary>
        /// The units of a period that's a constant, as literals, or a parameter, read from the JSON it's sent as. Null for
        /// anything else.
        /// </summary>
        private SqlExpression[] UnitsOf(SqlExpression period, bool negate)
        {
            var units = PeriodConvention.Units;
            switch (period)
            {
                case SqlConstantExpression { Value: Period constant }:
                    return units
                        .Select(unit => Convert.ToInt64(unit.GetValue(constant)) * (negate ? -1 : 1))
                        .Select((value, i) => i < 4
                            ? _sqlExpressionFactory.Constant((int)value, typeof(int), _intTypeMapping)
                            : _sqlExpressionFactory.Constant(value, typeof(long), _longTypeMapping))
                        .ToArray();

                case SqlParameterExpression parameter:
                    var json = _sqlExpressionFactory.ApplyTypeMapping(parameter, PeriodJsonTypeMapping.Default);
                    return units
                        .Select(unit =>
                        {
                            var mapping = unit.PropertyType == typeof(int) ? _intTypeMapping : _longTypeMapping;
                            var text = _sqlExpressionFactory.Function(
                                "JSON_VALUE",
                                new[] { json, _sqlExpressionFactory.Constant("$." + unit.Name, _stringTypeMapping) },
                                nullable: true,
                                argumentsPropagateNullability: new[] { true, false },
                                typeof(string),
                                _stringTypeMapping);
                            var number = _sqlExpressionFactory.Convert(text, unit.PropertyType, mapping);
                            return negate ? new SqlUnaryExpression(ExpressionType.Negate, number, unit.PropertyType, mapping) : number;
                        })
                        .ToArray();

                default:
                    return null;
            }
        }

        private SqlExpression PlusToDateTime(SqlExpression value, SqlExpression[] units)
        {
            var (days, nanoseconds) = TimeUnits(units);
            return DateAdd("day", AsInt(days), PlusTime(PlusDateUnits(AsDateTime2(value), units), nanoseconds));
        }

        private SqlExpression PlusToDate(SqlExpression value, SqlExpression[] units)
            => DateAdd("day", ErrorIf(AnyNonZero(units.Skip(4)), "A Period with time units can't be added to a LocalDate."), PlusDateUnits(value, units));

        private SqlExpression PlusToTime(SqlExpression value, SqlExpression[] units)
        {
            // A LocalTime wraps around midnight: the time units modulo a day, made positive, which DATEADD then wraps.
            var day = Integer(NodaConstants.NanosecondsPerDay);
            var withinDay = Long(ExpressionType.Modulo, Long(ExpressionType.Add, Long(ExpressionType.Modulo, TimeUnits(units).Nanoseconds, day), day), day);

            return DateAdd("second", ErrorIf(AnyNonZero(units.Take(4)), "A Period with date units can't be added to a LocalTime."), PlusTime(value, withinDay));
        }

        /// <summary>Years, then months, then weeks and days, as NodaTime adds them.</summary>
        private SqlExpression PlusDateUnits(SqlExpression value, SqlExpression[] units)
            => DateAdd("day", Days(units), DateAdd("month", units[1], DateAdd("year", units[0], value)));

        /// <summary>
        /// Nanoseconds (no more than a few days of them) as days, seconds and the rest truncated to 100ns, each of which fits
        /// the int DATEADD takes.
        /// </summary>
        private SqlExpression PlusTime(SqlExpression value, SqlExpression nanoseconds)
        {
            var subsecond = Long(
                ExpressionType.Multiply,
                Long(ExpressionType.Divide, Long(ExpressionType.Modulo, nanoseconds, Integer(NodaConstants.NanosecondsPerSecond)), Integer(NodaConstants.NanosecondsPerTick)),
                Integer(NodaConstants.NanosecondsPerTick));
            var seconds = Long(ExpressionType.Divide, Long(ExpressionType.Modulo, nanoseconds, Integer(NodaConstants.NanosecondsPerDay)), Integer(NodaConstants.NanosecondsPerSecond));
            var days = Long(ExpressionType.Divide, nanoseconds, Integer(NodaConstants.NanosecondsPerDay));

            var result = DateAdd("second", AsInt(seconds), DateAdd("nanosecond", AsInt(subsecond), value));
            return value.Type == typeof(LocalTime) ? result : DateAdd("day", AsInt(days), result);
        }

        /// <summary>
        /// In nanoseconds, as Duration arithmetic is: a bigint, so up to about 292 years, which NodaTime's Duration
        /// exceeds.
        /// </summary>
        private SqlExpression ToDuration(SqlExpression[] units)
        {
            var (timeDays, timeNanoseconds) = TimeUnits(units);
            var days = Long(ExpressionType.Add, _sqlExpressionFactory.Convert(Days(units), typeof(long), _longTypeMapping), timeDays);
            var nanoseconds = Long(ExpressionType.Add, Long(ExpressionType.Multiply, days, Integer(NodaConstants.NanosecondsPerDay)), timeNanoseconds);

            return new SqlBinaryExpression(
                ExpressionType.Add,
                nanoseconds,
                ErrorIf(AnyNonZero(units.Take(2)), "A Period with years or months has no fixed Duration."),
                typeof(Duration),
                DurationBigIntTypeMapping.Default);
        }

        private SqlExpression UnitsBetween(SqlExpression start, SqlExpression end, PeriodUnits unit, Type valueType)
        {
            if (valueType == typeof(LocalTime))
            {
                var nanoseconds = DateDiff("nanosecond", start, end, typeof(long), _longTypeMapping, "DATEDIFF_BIG");
                return TimeUnit(unit) is { } perUnit ? Long(ExpressionType.Divide, nanoseconds, Integer(perUnit)) : null;
            }

            switch (unit)
            {
                case PeriodUnits.Years:
                    return CalendarUnitsBetween("year", start, end);
                case PeriodUnits.Months:
                    return CalendarUnitsBetween("month", start, end);
                case PeriodUnits.Weeks:
                    return new SqlBinaryExpression(ExpressionType.Divide, DaysBetween(start, end, valueType), Integer(7), typeof(int), _intTypeMapping);
                case PeriodUnits.Days:
                    return DaysBetween(start, end, valueType);
            }

            // Time units between LocalDateTimes, from the difference in ticks, which a bigint holds for the whole range.
            if (valueType != typeof(LocalDateTime) || TimeUnit(unit) is not { } nanosecondsPerUnit)
                return null;

            var ticks = Long(ExpressionType.Subtract, _durations.Ticks(AsDateTime2(end)), _durations.Ticks(AsDateTime2(start)));
            return nanosecondsPerUnit == 1
                ? Long(ExpressionType.Multiply, ticks, Integer(NodaConstants.NanosecondsPerTick))
                : Long(ExpressionType.Divide, ticks, Integer(nanosecondsPerUnit / NodaConstants.NanosecondsPerTick));
        }

        private static long? TimeUnit(PeriodUnits unit) => unit switch
        {
            PeriodUnits.Hours => NodaConstants.NanosecondsPerHour,
            PeriodUnits.Minutes => NodaConstants.NanosecondsPerMinute,
            PeriodUnits.Seconds => NodaConstants.NanosecondsPerSecond,
            PeriodUnits.Milliseconds => NodaConstants.NanosecondsPerMillisecond,
            PeriodUnits.Ticks => NodaConstants.NanosecondsPerTick,
            PeriodUnits.Nanoseconds => 1,
            _ => null,
        };

        /// <summary>Between dates, DATEDIFF; between date and times, the same correction as for years and months.</summary>
        private SqlExpression DaysBetween(SqlExpression start, SqlExpression end, Type valueType)
            => valueType == typeof(LocalDate) ? DateDiff("day", start, end, typeof(int), _intTypeMapping) : CalendarUnitsBetween("day", start, end);

        /// <summary>
        /// The most whole units that can be added to start without passing end, toward zero:
        /// DATEDIFF, less one if adding that many passes end (or plus one, going backwards).
        /// </summary>
        private SqlExpression CalendarUnitsBetween(string datePart, SqlExpression start, SqlExpression end)
        {
            var difference = DateDiff(datePart, start, end, typeof(int), _intTypeMapping);
            var reached = DateAdd(datePart, difference, start);
            var correction = _sqlExpressionFactory.Case(
                new[]
                {
                    new CaseWhenClause(_sqlExpressionFactory.AndAlso(_sqlExpressionFactory.LessThanOrEqual(start, end), _sqlExpressionFactory.GreaterThan(reached, end)), Integer(-1)),
                    new CaseWhenClause(_sqlExpressionFactory.AndAlso(_sqlExpressionFactory.GreaterThan(start, end), _sqlExpressionFactory.LessThan(reached, end)), Integer(1)),
                },
                Integer(0));

            return new SqlBinaryExpression(ExpressionType.Add, difference, correction, typeof(int), _intTypeMapping);
        }

        /// <summary>Weeks and days as days.</summary>
        private SqlExpression Days(SqlExpression[] units)
            => new SqlBinaryExpression(
                ExpressionType.Add,
                new SqlBinaryExpression(ExpressionType.Multiply, units[2], Integer(7), typeof(int), _intTypeMapping),
                units[3],
                typeof(int),
                _intTypeMapping);

        /// <summary>
        /// The time units, hours to nanoseconds, as whole days and the nanoseconds left over: each unit's whole days, and
        /// the rest of it in nanoseconds. All in nanoseconds, 3,000,000 hours (about 342 years) would overflow a bigint.
        /// </summary>
        private (SqlExpression Days, SqlExpression Nanoseconds) TimeUnits(SqlExpression[] units)
        {
            var nanosecondsPerUnit = new[]
            {
                NodaConstants.NanosecondsPerHour, NodaConstants.NanosecondsPerMinute, NodaConstants.NanosecondsPerSecond,
                NodaConstants.NanosecondsPerMillisecond, NodaConstants.NanosecondsPerTick, 1L,
            };

            var parts = units.Skip(4)
                .Select((unit, i) =>
                {
                    var perDay = Integer(NodaConstants.NanosecondsPerDay / nanosecondsPerUnit[i]);
                    var rest = Long(ExpressionType.Modulo, unit, perDay);
                    return (Days: Long(ExpressionType.Divide, unit, perDay),
                        Nanoseconds: nanosecondsPerUnit[i] == 1 ? rest : Long(ExpressionType.Multiply, rest, Integer(nanosecondsPerUnit[i])));
                })
                .ToArray();

            return (parts.Select(p => p.Days).Aggregate((total, next) => Long(ExpressionType.Add, total, next)),
                parts.Select(p => p.Nanoseconds).Aggregate((total, next) => Long(ExpressionType.Add, total, next)));
        }

        /// <summary>
        /// Whether any of the units isn't zero: HasDateComponent, HasTimeComponent, and the conditions of
        /// <see cref="ErrorIf"/>. A NULL unit (of a null period) counts as zero: EF Core would otherwise make NULL &lt;&gt; 0
        /// true, as null != 0 is in C#, so a null period would have time units, and fail where its result is just NULL.
        /// </summary>
        private SqlExpression AnyNonZero(IEnumerable<SqlExpression> units)
            => units
                .Select(unit => _sqlExpressionFactory.NotEqual(_sqlExpressionFactory.Coalesce(unit, Integer(0)), Integer(0)))
                .Aggregate((any, next) => _sqlExpressionFactory.OrElse(any, next));

        /// <summary>
        /// Where NodaTime throws, the SQL fails too: zero, added to the result, unless the condition holds, when converting
        /// the message to a number fails, and SQL Server's error includes it ("Conversion failed when converting the
        /// nvarchar value '...' to data type int"). It depends on the row, so SQL Server doesn't work it out beforehand, as
        /// it may a constant, failing for rows where the condition doesn't hold.
        /// </summary>
        private SqlExpression ErrorIf(SqlExpression condition, string message)
            => _sqlExpressionFactory.Coalesce(
                _sqlExpressionFactory.Convert(
                    _sqlExpressionFactory.Case(new[] { new CaseWhenClause(condition, _sqlExpressionFactory.Constant(message, _stringTypeMapping)) }, elseResult: null),
                    typeof(int),
                    _intTypeMapping),
                Integer(0));

        /// <summary>A datetime or smalldatetime as a datetime2, which DATEADD can add nanoseconds to.</summary>
        private SqlExpression AsDateTime2(SqlExpression value)
            => value.TypeMapping?.StoreType.StartsWith("datetime2", StringComparison.OrdinalIgnoreCase) == true
                ? value
                : _sqlExpressionFactory.Convert(value, typeof(LocalDateTime), _typeMappingSource.FindMapping(typeof(LocalDateTime), "datetime2"));

        private SqlExpression DateAdd(string datePart, SqlExpression number, SqlExpression value)
            => _sqlExpressionFactory.Function(
                "DATEADD",
                new[] { new SqlFragmentExpression(datePart), number, value },
                nullable: true,
                argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                value.Type,
                value.TypeMapping);

        private SqlExpression DateDiff(string datePart, SqlExpression start, SqlExpression end, Type type, RelationalTypeMapping typeMapping, string function = "DATEDIFF")
            => _sqlExpressionFactory.Function(
                function,
                new[] { new SqlFragmentExpression(datePart), start, end },
                nullable: true,
                argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                type,
                typeMapping);

        private SqlExpression Long(ExpressionType operatorType, SqlExpression left, SqlExpression right)
            => new SqlBinaryExpression(operatorType, left, right, typeof(long), _longTypeMapping);

        private SqlExpression AsInt(SqlExpression value) => _sqlExpressionFactory.Convert(value, typeof(int), _intTypeMapping);

        private SqlExpression Integer(long value) => _durations.Integer(value);
    }

    internal sealed class PeriodMethodCallTranslatorPlugin : IMethodCallTranslatorPlugin
    {
        public PeriodMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
        {
            Translators = new IMethodCallTranslator[] { new PeriodTranslator(sqlExpressionFactory, typeMappingSource) };
        }

        public IEnumerable<IMethodCallTranslator> Translators { get; }
    }
}
