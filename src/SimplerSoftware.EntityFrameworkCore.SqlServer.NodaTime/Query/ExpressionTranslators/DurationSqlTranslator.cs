using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Diagnostics;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>How a <see cref="Duration"/> value is stored, which decides how it is translated (#38).</summary>
    internal enum DurationStorage
    {
        /// <summary>time, the default: 0 to just under 24 hours.</summary>
        Time,

        /// <summary>bigint holding nanoseconds: HasColumnType("bigint").</summary>
        Nanoseconds,

        /// <summary>int holding whole seconds: HasColumnType("int").</summary>
        Seconds,

        /// <summary>datetime2 holding the offset from 1900-01-01: HasColumnType("datetime2").</summary>
        DateTime2,

        /// <summary>Fixed-width text: HasColumnType("nvarchar(28)") etc. Only equality is translated.</summary>
        String,

        /// <summary>Anything else, such as the user's own value converter. Translated as it was before #38.</summary>
        Other,
    }

    /// <summary>
    /// Builds the SQL for <see cref="Duration"/> values stored as time, bigint (nanoseconds), int (seconds) or
    /// datetime2 (an offset from 1900-01-01) (#38).
    /// <para>
    /// Arithmetic results are always nanoseconds in a bigint, whatever the storage: they can then pass 24 hours or
    /// go negative, keep the full precision, and read back correctly. Durations stored differently are compared by
    /// converting both to nanoseconds, or a time to a datetime2 when the other is one.
    /// </para>
    /// </summary>
    internal sealed class DurationSqlTranslator
    {

        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly RelationalTypeMapping _longTypeMapping;
        private readonly RelationalTypeMapping _intTypeMapping;
        private readonly RelationalTypeMapping _doubleTypeMapping;

        public DurationSqlTranslator(ISqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _longTypeMapping = DefaultTypeMapping(0L);
            _intTypeMapping = DefaultTypeMapping(0);
            _doubleTypeMapping = DefaultTypeMapping(0d);
        }

        private RelationalTypeMapping DefaultTypeMapping(object value)
            => _sqlExpressionFactory.ApplyDefaultTypeMapping(_sqlExpressionFactory.Constant(value)).TypeMapping;

        /// <summary>A Duration expression without a type mapping yet (e.g. a parameter) gets Duration's default one, time.</summary>
        public static DurationStorage StorageOf(RelationalTypeMapping typeMapping) => typeMapping switch
        {
            null => DurationStorage.Time,
            DurationBigIntTypeMapping => DurationStorage.Nanoseconds,
            DurationIntTypeMapping => DurationStorage.Seconds,
            DurationDateTime2TypeMapping => DurationStorage.DateTime2,
            DurationStringTypeMapping => DurationStorage.String,
            DurationTypeMapping => DurationStorage.Time,
            // e.g. the user's own converter to TimeSpan
            _ when string.Equals(typeMapping.StoreTypeNameBase?.Trim(), SqlServerDateTimeTypes.Time, StringComparison.OrdinalIgnoreCase) => DurationStorage.Time,
            _ => DurationStorage.Other,
        };

        public static DurationStorage StorageOf(SqlExpression duration) => StorageOf(duration.TypeMapping);

        /// <summary>Whether Days, Hours and so on are computed with integer arithmetic; DATEPART only works for a time.</summary>
        public static bool HasArithmeticComponents(DurationStorage storage)
            => storage is DurationStorage.Nanoseconds or DurationStorage.Seconds or DurationStorage.DateTime2;

        /// <summary>Whether the duration is stored as text, for which only equality is translated.</summary>
        public static bool IsText(SqlExpression duration)
            => duration?.TypeMapping != null && StorageOf(duration) == DurationStorage.String;

        /// <summary>
        /// The duration as a number of nanoseconds, typed as a Duration stored in a bigint; null if its storage isn't
        /// one we know how to convert. A parameter or constant without a type mapping yet is simply sent as
        /// nanoseconds, so it can be 24 hours or more.
        /// </summary>
        public SqlExpression ToNanoseconds(SqlExpression duration)
        {
            if (duration.TypeMapping == null)
                return _sqlExpressionFactory.ApplyTypeMapping(duration, DurationBigIntTypeMapping.Default);

            switch (StorageOf(duration))
            {
                case DurationStorage.Nanoseconds:
                    return duration;

                case DurationStorage.Seconds:
                    return new SqlBinaryExpression(
                        ExpressionType.Multiply,
                        _sqlExpressionFactory.Convert(duration, typeof(long), _longTypeMapping),
                        Integer(NodaConstants.NanosecondsPerSecond),
                        typeof(Duration),
                        DurationBigIntTypeMapping.Default);

                case DurationStorage.Time:
                case DurationStorage.DateTime2:
                    // SQL Server treats a time value as being on 1900-01-01, so for a time this counts the nanoseconds
                    // since midnight; a datetime2 holds the offset from 1900-01-01.
                    return _sqlExpressionFactory.Function(
                        "DATEDIFF_BIG",
                        new SqlExpression[]
                        {
                            new SqlFragmentExpression("nanosecond"),
                            new SqlFragmentExpression("'19000101'"),
                            duration,
                        },
                        nullable: true,
                        argumentsPropagateNullability: SqlServerFunctionNullability.DateDiffFrom1900,
                        typeof(Duration),
                        DurationBigIntTypeMapping.Default);

                default:
                    return null;
            }
        }

        /// <summary>
        /// A component such as Hours: (duration / unit) % modulo. NodaTime truncates each component toward zero and
        /// gives it the duration's sign (-26h03m has Days -1, Hours -2, Minutes -3), as T-SQL's integer / and % do.
        /// </summary>
        public SqlExpression Component(SqlExpression duration, long nanosecondsPerUnit, long? modulo, Type returnType)
        {
            // Work in the storage's own unit where possible (whole seconds for int, ticks for datetime2): it can't
            // overflow, unlike nanoseconds past about 292 years.
            SqlExpression value;
            long unit;
            Type type;
            RelationalTypeMapping typeMapping;
            switch (StorageOf(duration))
            {
                case DurationStorage.Seconds:
                    (value, unit, type, typeMapping) = (duration, NodaConstants.NanosecondsPerSecond, returnType, _intTypeMapping);
                    break;
                case DurationStorage.DateTime2:
                    (value, unit, type, typeMapping) = (Ticks(duration), NodaConstants.NanosecondsPerTick, typeof(long), _longTypeMapping);
                    break;
                default:
                    (value, unit, type, typeMapping) = (ToNanoseconds(duration), 1, typeof(long), _longTypeMapping);
                    break;
            }

            if (value == null)
                return null;

            if (nanosecondsPerUnit % unit == 0)
            {
                // e.g. Hours from ticks: ticks / 36000000000 % 24
                var divisor = nanosecondsPerUnit / unit;
                if (divisor != 1)
                    value = new SqlBinaryExpression(ExpressionType.Divide, value, Integer(divisor), type, typeMapping);
                if (modulo.HasValue)
                    value = new SqlBinaryExpression(ExpressionType.Modulo, value, Integer(modulo.Value), type, typeMapping);
            }
            else
            {
                // A component finer than the unit, e.g. SubsecondNanoseconds from ticks: ticks % 10000000 * 100.
                // (Every such component has a modulo that is a whole number of units.)
                value = new SqlBinaryExpression(ExpressionType.Modulo, value, Integer(modulo.Value * nanosecondsPerUnit / unit), type, typeMapping);
                value = new SqlBinaryExpression(ExpressionType.Multiply, value, Integer(unit / nanosecondsPerUnit), type, typeMapping);
            }

            return type == typeof(long)
                ? _sqlExpressionFactory.Convert(value, returnType, _intTypeMapping)
                : value;
        }

        /// <summary>
        /// A datetime2's offset from 1900-01-01 in ticks (100ns), which a bigint holds for the whole datetime2 range:
        /// DATEDIFF_BIG(microsecond, '19000101', x) * 10 + DATEPART(nanosecond, x) / 100 % 10. DATEDIFF counts the
        /// microsecond boundaries crossed, rounding down, and DATEPART gives the rest, which is never negative.
        /// </summary>
        private SqlExpression Ticks(SqlExpression dateTime2)
        {
            var microseconds = _sqlExpressionFactory.Function(
                "DATEDIFF_BIG",
                new SqlExpression[] { new SqlFragmentExpression("microsecond"), new SqlFragmentExpression("'19000101'"), dateTime2 },
                nullable: true,
                argumentsPropagateNullability: SqlServerFunctionNullability.DateDiffFrom1900,
                typeof(long),
                _longTypeMapping);

            var nanosecondPart = _sqlExpressionFactory.Function(
                "DATEPART",
                new SqlExpression[] { new SqlFragmentExpression("nanosecond"), dateTime2 },
                nullable: true,
                argumentsPropagateNullability: SqlServerFunctionNullability.DatePart,
                typeof(int),
                _intTypeMapping);

            return new SqlBinaryExpression(
                ExpressionType.Add,
                new SqlBinaryExpression(ExpressionType.Multiply, microseconds, Integer(10), typeof(long), _longTypeMapping),
                new SqlBinaryExpression(
                    ExpressionType.Modulo,
                    new SqlBinaryExpression(ExpressionType.Divide, nanosecondPart, Integer(100), typeof(int), _intTypeMapping),
                    Integer(10),
                    typeof(int),
                    _intTypeMapping),
                typeof(long),
                _longTypeMapping);
        }

        /// <summary>
        /// PlusHours etc.: the duration plus amount units. Like NodaTime's Duration.FromHours(double), the amount is
        /// multiplied out to nanoseconds as a float and truncated toward zero, so fractions such as 1.5 hours are kept.
        /// Null if the duration's storage isn't one we know how to convert.
        /// </summary>
        public SqlExpression Plus(SqlExpression duration, SqlExpression amount, long nanosecondsPerUnit)
        {
            var nanoseconds = ToNanoseconds(duration);
            if (nanoseconds == null)
                return null;

            var scaledAmount = nanosecondsPerUnit == 1
                ? amount
                : _sqlExpressionFactory.Multiply(amount, _sqlExpressionFactory.Constant((double)nanosecondsPerUnit, typeof(double), _doubleTypeMapping));

            return new SqlBinaryExpression(
                ExpressionType.Add,
                nanoseconds,
                _sqlExpressionFactory.Convert(scaledAmount, typeof(long), _longTypeMapping),
                typeof(Duration),
                DurationBigIntTypeMapping.Default);
        }

        /// <summary>
        /// EF.Functions.DateDiffHour etc. for durations that aren't both time: the number of unit boundaries crossed,
        /// as SQL Server's DATEDIFF counts them, i.e. floor(end / unit) - floor(start / unit).
        /// Null if either storage isn't one we know how to convert.
        /// </summary>
        public SqlExpression DateDiff(SqlExpression start, SqlExpression end, long nanosecondsPerUnit, Type returnType)
        {
            var startNanoseconds = ToNanoseconds(start);
            var endNanoseconds = ToNanoseconds(end);
            if (startNanoseconds == null || endNanoseconds == null)
                return null;

            var isBig = Nullable.GetUnderlyingType(returnType) == typeof(long) || returnType == typeof(long);
            var difference = new SqlBinaryExpression(
                ExpressionType.Subtract,
                FloorDivide(endNanoseconds, nanosecondsPerUnit),
                FloorDivide(startNanoseconds, nanosecondsPerUnit),
                isBig ? returnType : typeof(long),
                _longTypeMapping);

            // DATEDIFF fails when the result doesn't fit an int; so does this cast.
            return isBig ? difference : _sqlExpressionFactory.Convert(difference, returnType, _intTypeMapping);
        }

        /// <summary>
        /// A comparison between two durations. When they are stored differently (e.g. time and bigint, or the result of
        /// PlusHours and a time column), both are converted to the same storage; otherwise it is the plain comparison.
        /// </summary>
        public SqlExpression Compare(ExpressionType operatorType, SqlExpression left, SqlExpression right, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            var common = CommonStorage(new[] { left, right }, out var incompatible);
            if (incompatible)
                return null;

            // Text: only = and <>, with other text or with a parameter or constant, which is formatted the same way.
            // Text order only matches duration order for values that aren't negative, so < and > aren't translated.
            if ((IsText(left) || IsText(right)) && operatorType is not (ExpressionType.Equal or ExpressionType.NotEqual))
                return null;

            if (common != null)
            {
                left = ConvertTo(left, common.Value);
                right = ConvertTo(right, common.Value);
            }
            else if (left.TypeMapping == null || right.TypeMapping == null)
            {
                // A side without a type mapping (a parameter or constant) takes the other side's, as usual, so that an
                // index on the column can still be used. Against int, that truncates it to whole seconds.
                WarnIfTruncatedToSeconds(new[] { left, right }, logger);
            }

            return _sqlExpressionFactory.MakeBinary(operatorType, left, right, typeMapping: null);
        }

        /// <summary>
        /// Duration + Duration or Duration - Duration, in nanoseconds. Two bigints are added as they are; time can't be
        /// added in SQL Server, and int would truncate a parameter to whole seconds and overflow past about 68 years.
        /// </summary>
        public SqlExpression Arithmetic(ExpressionType operatorType, SqlExpression left, SqlExpression right)
        {
            var leftNanoseconds = ToNanoseconds(left);
            var rightNanoseconds = ToNanoseconds(right);
            if (leftNanoseconds != null && rightNanoseconds != null)
                return new SqlBinaryExpression(operatorType, leftNanoseconds, rightNanoseconds, typeof(Duration), DurationBigIntTypeMapping.Default);

            // The user's own converter: translated as before. Text: not translated.
            return (IsOther(left) || IsOther(right)) && !IsText(left) && !IsText(right)
                ? _sqlExpressionFactory.MakeBinary(operatorType, left, right, typeMapping: null)
                : null;
        }

        /// <summary>-duration, in nanoseconds. The user's own converter is translated as before; text is not translated.</summary>
        public SqlExpression Negate(SqlExpression duration)
        {
            var nanoseconds = ToNanoseconds(duration);
            if (nanoseconds != null)
                return new SqlUnaryExpression(ExpressionType.Negate, nanoseconds, typeof(Duration), DurationBigIntTypeMapping.Default);

            return IsOther(duration) ? _sqlExpressionFactory.Negate(duration) : null;
        }

        /// <summary>
        /// duration * number or duration / number, in nanoseconds. NodaTime truncates a division toward zero, as T-SQL
        /// does. The user's own converter is translated as before; text is not translated.
        /// </summary>
        public SqlExpression Scale(ExpressionType operatorType, SqlExpression duration, SqlExpression number)
        {
            var nanoseconds = ToNanoseconds(duration);
            if (nanoseconds != null)
            {
                return new SqlBinaryExpression(
                    operatorType,
                    nanoseconds,
                    _sqlExpressionFactory.ApplyTypeMapping(number, _longTypeMapping),
                    typeof(Duration),
                    DurationBigIntTypeMapping.Default);
            }

            return IsOther(duration) ? _sqlExpressionFactory.MakeBinary(operatorType, duration, number, typeMapping: null) : null;
        }

        /// <summary>
        /// A value that ExecuteUpdate writes into a column, converted to the column's storage. Into int it is truncated
        /// to whole seconds, as a saved value is. Into time, a value of 24 hours or more, or a negative one, raises an
        /// error, as saving it would; DATEADD on a time used to wrap it around instead.
        /// </summary>
        public SqlExpression ForColumn(SqlExpression value, SqlExpression column)
        {
            var columnTypeMapping = column.TypeMapping;
            if (columnTypeMapping == null)
                return value;
            if (value.TypeMapping == null)
                return _sqlExpressionFactory.ApplyTypeMapping(value, columnTypeMapping);

            var valueStorage = StorageOf(value);
            var columnStorage = StorageOf(column);
            if (valueStorage == columnStorage)
                return value;

            // Text can't be converted to or from another storage in SQL, not even the user's own.
            if (valueStorage == DurationStorage.String || columnStorage == DurationStorage.String)
                return null;

            // The user's own converter: written as it is, as before.
            if (valueStorage == DurationStorage.Other || columnStorage == DurationStorage.Other)
                return value;

            if (columnStorage == DurationStorage.DateTime2 && valueStorage is DurationStorage.Time or DurationStorage.Seconds)
                return AsDateTime2(value, columnTypeMapping);

            var nanoseconds = ToNanoseconds(value);
            switch (columnStorage)
            {
                case DurationStorage.Nanoseconds:
                    return nanoseconds;

                case DurationStorage.Seconds:
                    return new SqlUnaryExpression(ExpressionType.Convert, WholeSeconds(nanoseconds), typeof(Duration), columnTypeMapping);

                case DurationStorage.DateTime2:
                    return NanosecondsToDateTime2(nanoseconds, columnTypeMapping);

                default:
                    // DATEADD(nanosecond, the rest truncated to 100ns, DATEADD(second, whole seconds, a time of 00:00)).
                    // DATEADD itself would round the nanoseconds to 100ns, but saving a value truncates them.
                    // SQL has no way to raise an error inside an expression, so a value out of range converts text to int
                    // instead, which fails with the text in the message. NULL passes through.
                    var outOfRange = _sqlExpressionFactory.OrElse(
                        _sqlExpressionFactory.LessThan(nanoseconds, Integer(0)),
                        _sqlExpressionFactory.GreaterThanOrEqual(nanoseconds, Integer(NodaConstants.NanosecondsPerDay)));
                    var checkedSeconds = _sqlExpressionFactory.Case(
                        new[]
                        {
                            new CaseWhenClause(
                                outOfRange,
                                _sqlExpressionFactory.Convert(
                                    _sqlExpressionFactory.Constant("A Duration of 24 hours or more, or a negative one, cannot be stored in a time column."),
                                    typeof(int),
                                    _intTypeMapping)),
                        },
                        _sqlExpressionFactory.Convert(WholeSeconds(nanoseconds), typeof(int), _intTypeMapping));
                    var subsecondNanoseconds = new SqlBinaryExpression(ExpressionType.Modulo, nanoseconds, Integer(NodaConstants.NanosecondsPerSecond), typeof(long), _longTypeMapping);
                    var subsecondTicks = new SqlBinaryExpression(
                        ExpressionType.Multiply,
                        new SqlBinaryExpression(ExpressionType.Divide, subsecondNanoseconds, Integer(100), typeof(long), _longTypeMapping),
                        Integer(100),
                        typeof(long),
                        _longTypeMapping);

                    var time = _sqlExpressionFactory.Function(
                        "DATEADD",
                        new SqlExpression[]
                        {
                            new SqlFragmentExpression("second"),
                            checkedSeconds,
                            new SqlFragmentExpression("CAST('00:00:00' AS time)"),
                        },
                        nullable: true,
                        argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                        typeof(Duration),
                        columnTypeMapping);

                    return _sqlExpressionFactory.Function(
                        "DATEADD",
                        new SqlExpression[]
                        {
                            new SqlFragmentExpression("nanosecond"),
                            _sqlExpressionFactory.Convert(subsecondTicks, typeof(int), _intTypeMapping),
                            time,
                        },
                        nullable: true,
                        argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                        typeof(Duration),
                        columnTypeMapping);
            }
        }

        /// <summary>
        /// A COALESCE or CASE between Durations (from ?? or ?:). When its branches are stored differently, they are all
        /// converted to the same storage, since SQL needs them the same; otherwise it is left as it is.
        /// </summary>
        public SqlExpression SameStorage(SqlExpression expression, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            switch (expression)
            {
                case SqlFunctionExpression { Name: "COALESCE" } coalesce:
                {
                    var common = CommonStorage(coalesce.Arguments, out var incompatible);
                    if (incompatible)
                        return null;
                    if (common == null)
                    {
                        WarnIfTruncatedToSeconds(coalesce.Arguments, logger);
                        return expression;
                    }

                    return coalesce
                        .Update(coalesce.Instance, coalesce.Arguments.Select(a => ConvertTo(a, common.Value)).ToList())
                        .ApplyTypeMapping(TypeMappingOf(common.Value));
                }

                case CaseExpression caseExpression:
                {
                    var results = caseExpression.WhenClauses.Select(c => c.Result).Append(caseExpression.ElseResult).Where(r => r != null).ToList();
                    var common = CommonStorage(results, out var incompatible);
                    if (incompatible)
                        return null;
                    if (common == null)
                    {
                        WarnIfTruncatedToSeconds(results, logger);
                        return expression;
                    }

                    return caseExpression.Update(
                        caseExpression.Operand,
                        caseExpression.WhenClauses.Select(c => new CaseWhenClause(c.Test, ConvertTo(c.Result, common.Value))).ToList(),
                        caseExpression.ElseResult == null ? null : ConvertTo(caseExpression.ElseResult, common.Value));
                }

                default:
                    return expression;
            }
        }

        /// <summary>
        /// The storage that durations stored differently should all be converted to. A datetime2 alongside time or int
        /// values: datetime2, since those always fit one; that's exact, works for the whole datetime2 range, and an index
        /// on the datetime2 can still be used. Otherwise nanoseconds. Null if they are all stored the same way, or one is
        /// stored in a way we don't know (the user's own converter). <paramref name="incompatible"/> is set when text is
        /// mixed with another storage, which SQL can't convert, so the expression can't be translated.
        /// Expressions without a type mapping yet (parameters and constants) are left out.
        /// </summary>
        private static DurationStorage? CommonStorage(IEnumerable<SqlExpression> durations, out bool incompatible)
        {
            var storages = durations.Where(d => d.TypeMapping != null).Select(StorageOf).Distinct().ToList();
            incompatible = storages.Count > 1 && storages.Contains(DurationStorage.String);
            if (storages.Count <= 1 || incompatible || storages.Contains(DurationStorage.Other))
                return null;

            return storages.Contains(DurationStorage.DateTime2)
                && storages.All(s => s is DurationStorage.Time or DurationStorage.DateTime2 or DurationStorage.Seconds)
                ? DurationStorage.DateTime2
                : DurationStorage.Nanoseconds;
        }

        private static RelationalTypeMapping TypeMappingOf(DurationStorage storage)
            => storage == DurationStorage.DateTime2 ? DurationDateTime2TypeMapping.Default : DurationBigIntTypeMapping.Default;

        /// <summary>The duration in the given storage, which is datetime2 or nanoseconds (see <see cref="CommonStorage"/>).</summary>
        private SqlExpression ConvertTo(SqlExpression duration, DurationStorage storage)
        {
            if (storage == DurationStorage.Nanoseconds)
                return ToNanoseconds(duration);

            if (duration.TypeMapping == null)
                return _sqlExpressionFactory.ApplyTypeMapping(duration, DurationDateTime2TypeMapping.Default);

            return StorageOf(duration) == DurationStorage.DateTime2 ? duration : AsDateTime2(duration, DurationDateTime2TypeMapping.Default);
        }

        /// <summary>
        /// A time or int duration as a datetime2 offset from 1900-01-01. A time: CAST, since SQL Server puts a time on
        /// 1900-01-01. Whole seconds: DATEADD(second, x, '19000101'); an int always fits.
        /// </summary>
        private SqlExpression AsDateTime2(SqlExpression duration, RelationalTypeMapping typeMapping)
            => StorageOf(duration) == DurationStorage.Time
                ? new SqlUnaryExpression(ExpressionType.Convert, duration, typeof(Duration), typeMapping)
                : _sqlExpressionFactory.Function(
                    "DATEADD",
                    new SqlExpression[] { new SqlFragmentExpression("second"), duration, new SqlFragmentExpression("CAST('19000101' AS datetime2)") },
                    nullable: true,
                    argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                    typeof(Duration),
                    typeMapping);

        private SqlExpression WholeSeconds(SqlExpression nanoseconds)
            => new SqlBinaryExpression(ExpressionType.Divide, nanoseconds, Integer(NodaConstants.NanosecondsPerSecond), typeof(long), _longTypeMapping);

        /// <summary>
        /// Nanoseconds as a datetime2 offset from 1900-01-01: whole days, then seconds, then the rest truncated to 100ns
        /// (as saving a value truncates it; DATEADD would round). Each step fits the int DATEADD takes.
        /// </summary>
        private SqlExpression NanosecondsToDateTime2(SqlExpression nanoseconds, RelationalTypeMapping typeMapping)
        {
            SqlExpression Part(long divisor, long? modulo, long truncateTo = 1)
            {
                SqlExpression part = nanoseconds;
                if (modulo.HasValue)
                    part = new SqlBinaryExpression(ExpressionType.Modulo, part, Integer(modulo.Value), typeof(long), _longTypeMapping);
                if (divisor != 1)
                    part = new SqlBinaryExpression(ExpressionType.Divide, part, Integer(divisor), typeof(long), _longTypeMapping);
                if (truncateTo != 1)
                    part = new SqlBinaryExpression(
                        ExpressionType.Multiply,
                        new SqlBinaryExpression(ExpressionType.Divide, part, Integer(truncateTo), typeof(long), _longTypeMapping),
                        Integer(truncateTo),
                        typeof(long),
                        _longTypeMapping);
                return _sqlExpressionFactory.Convert(part, typeof(int), _intTypeMapping);
            }

            SqlExpression DateAdd(string datePart, SqlExpression number, SqlExpression date)
                => _sqlExpressionFactory.Function(
                    "DATEADD",
                    new[] { new SqlFragmentExpression(datePart), number, date },
                    nullable: true,
                    argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                    typeof(Duration),
                    typeMapping);

            var days = DateAdd("day", Part(NodaConstants.NanosecondsPerDay, null), new SqlFragmentExpression("CAST('19000101' AS datetime2)"));
            var seconds = DateAdd("second", Part(NodaConstants.NanosecondsPerSecond, NodaConstants.NanosecondsPerDay), days);
            return DateAdd("nanosecond", Part(1, NodaConstants.NanosecondsPerSecond, truncateTo: NodaConstants.NanosecondsPerTick), seconds);
        }

        /// <summary>
        /// Logs a warning when a parameter or constant takes, or has already been given (as in COALESCE), the int
        /// mapping of a Duration stored as whole seconds, and so loses any fraction of a second. A constant is only
        /// reported if it has one; a parameter's value isn't known yet.
        /// </summary>
        private static void WarnIfTruncatedToSeconds(IReadOnlyCollection<SqlExpression> durations, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            var seconds = durations.FirstOrDefault(d => !IsValue(d) && d.TypeMapping != null && StorageOf(d) == DurationStorage.Seconds);
            if (seconds == null)
                return;

            var mayLoseAFraction = durations.Any(d => (d.TypeMapping == null || StorageOf(d) == DurationStorage.Seconds) && d switch
            {
                SqlConstantExpression { Value: Duration constant } => constant.SubsecondNanoseconds != 0,
                SqlConstantExpression => false,
                SqlParameterExpression => true,
                _ => d.TypeMapping == null,
            });

            if (mayLoseAFraction)
                logger.DurationValueTruncatedToSeconds(seconds is ColumnExpression column ? $"{column.TableAlias}.{column.Name}" : "value");
        }

        private static bool IsValue(SqlExpression expression) => expression is SqlParameterExpression or SqlConstantExpression;

        private static bool IsOther(SqlExpression duration)
            => duration.TypeMapping != null && StorageOf(duration) == DurationStorage.Other;

        /// <summary>
        /// floor(value / divisor) for a bigint. T-SQL's / truncates toward zero, which differs for negative values:
        /// value / divisor - CASE WHEN value % divisor &lt; 0 THEN 1 ELSE 0 END.
        /// </summary>
        private SqlExpression FloorDivide(SqlExpression value, long divisor)
        {
            if (divisor == 1)
                return value;

            var quotient = new SqlBinaryExpression(ExpressionType.Divide, value, Integer(divisor), typeof(long), _longTypeMapping);
            var remainder = new SqlBinaryExpression(ExpressionType.Modulo, value, Integer(divisor), typeof(long), _longTypeMapping);
            var roundDown = _sqlExpressionFactory.Case(
                new[] { new CaseWhenClause(_sqlExpressionFactory.LessThan(remainder, Integer(0)), Integer(1)) },
                Integer(0));

            return new SqlBinaryExpression(ExpressionType.Subtract, quotient, roundDown, typeof(long), _longTypeMapping);
        }

        /// <summary>
        /// An int literal when the value fits, otherwise CAST(... AS bigint): SQL Server types a larger literal as
        /// numeric, and bigint / numeric is decimal division, not integer division.
        /// </summary>
        private SqlExpression Integer(long value)
            => value >= int.MinValue && value <= int.MaxValue
                ? _sqlExpressionFactory.Constant((int)value, typeof(int), _intTypeMapping)
                : _sqlExpressionFactory.Constant(value, typeof(long), _longTypeMapping);
    }
}
