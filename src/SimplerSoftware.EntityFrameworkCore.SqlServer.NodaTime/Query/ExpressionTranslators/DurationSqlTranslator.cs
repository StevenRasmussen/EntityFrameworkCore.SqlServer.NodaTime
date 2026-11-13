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

        /// <summary>Anything else, such as the user's own value converter. Translated as it was before #38.</summary>
        Other,
    }

    /// <summary>
    /// Builds the SQL for <see cref="Duration"/> values stored as time, bigint (nanoseconds) or int (seconds) (#38).
    /// <para>
    /// Arithmetic results are always nanoseconds in a bigint, whatever the storage: they can then pass 24 hours or
    /// go negative, keep the full precision, and read back correctly. Durations stored differently are compared by
    /// converting both to nanoseconds.
    /// </para>
    /// </summary>
    internal sealed class DurationSqlTranslator
    {
        private static readonly bool[] _dateDiffPropagatesNullability = { false, false, true };
        private static readonly bool[] _dateAddPropagatesNullability = { false, true, true };

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
            DurationTypeMapping => DurationStorage.Time,
            // e.g. the user's own converter to TimeSpan
            _ when string.Equals(typeMapping.StoreTypeNameBase?.Trim(), SqlServerDateTimeTypes.Time, StringComparison.OrdinalIgnoreCase) => DurationStorage.Time,
            _ => DurationStorage.Other,
        };

        public static DurationStorage StorageOf(SqlExpression duration) => StorageOf(duration.TypeMapping);

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
                    // SQL Server treats a time value as being on 1900-01-01, so this counts the nanoseconds since midnight.
                    return _sqlExpressionFactory.Function(
                        "DATEDIFF_BIG",
                        new SqlExpression[]
                        {
                            new SqlFragmentExpression("nanosecond"),
                            new SqlFragmentExpression("'19000101'"),
                            duration,
                        },
                        nullable: true,
                        argumentsPropagateNullability: _dateDiffPropagatesNullability,
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
            SqlExpression value;
            long divisor;
            Type type;
            RelationalTypeMapping typeMapping;

            if (StorageOf(duration) == DurationStorage.Seconds && nanosecondsPerUnit % NodaConstants.NanosecondsPerSecond == 0)
            {
                // Whole seconds: work in the int itself.
                value = duration;
                divisor = nanosecondsPerUnit / NodaConstants.NanosecondsPerSecond;
                type = returnType;
                typeMapping = _intTypeMapping;
            }
            else
            {
                value = ToNanoseconds(duration);
                if (value == null)
                    return null;
                divisor = nanosecondsPerUnit;
                type = typeof(long);
                typeMapping = _longTypeMapping;
            }

            if (divisor != 1)
                value = new SqlBinaryExpression(ExpressionType.Divide, value, Integer(divisor), type, typeMapping);
            if (modulo.HasValue)
                value = new SqlBinaryExpression(ExpressionType.Modulo, value, Integer(modulo.Value), type, typeMapping);

            return type == typeof(long)
                ? _sqlExpressionFactory.Convert(value, returnType, _intTypeMapping)
                : value;
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
        /// PlusHours and a time column), both are converted to nanoseconds; otherwise it is the plain comparison.
        /// </summary>
        public SqlExpression Compare(ExpressionType operatorType, SqlExpression left, SqlExpression right, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (left.TypeMapping != null && right.TypeMapping != null)
            {
                var leftStorage = StorageOf(left);
                var rightStorage = StorageOf(right);
                if (leftStorage != rightStorage && leftStorage != DurationStorage.Other && rightStorage != DurationStorage.Other)
                {
                    left = ToNanoseconds(left);
                    right = ToNanoseconds(right);
                }
            }
            else
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
            // The user's own converter: translated as before.
            if (IsOther(left) || IsOther(right))
                return _sqlExpressionFactory.MakeBinary(operatorType, left, right, typeMapping: null);

            return new SqlBinaryExpression(operatorType, ToNanoseconds(left), ToNanoseconds(right), typeof(Duration), DurationBigIntTypeMapping.Default);
        }

        /// <summary>-duration, in nanoseconds.</summary>
        public SqlExpression Negate(SqlExpression duration)
            => IsOther(duration)
                ? _sqlExpressionFactory.Negate(duration)
                : new SqlUnaryExpression(ExpressionType.Negate, ToNanoseconds(duration), typeof(Duration), DurationBigIntTypeMapping.Default);

        /// <summary>
        /// duration * number or duration / number, in nanoseconds. NodaTime truncates a division toward zero, as T-SQL
        /// does. The user's own converter is translated as before.
        /// </summary>
        public SqlExpression Scale(ExpressionType operatorType, SqlExpression duration, SqlExpression number)
            => IsOther(duration)
                ? _sqlExpressionFactory.MakeBinary(operatorType, duration, number, typeMapping: null)
                : new SqlBinaryExpression(
                    operatorType,
                    ToNanoseconds(duration),
                    _sqlExpressionFactory.ApplyTypeMapping(number, _longTypeMapping),
                    typeof(Duration),
                    DurationBigIntTypeMapping.Default);

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
            if (valueStorage == columnStorage || valueStorage == DurationStorage.Other || columnStorage == DurationStorage.Other)
                return value;

            var nanoseconds = ToNanoseconds(value);
            var wholeSeconds = new SqlBinaryExpression(ExpressionType.Divide, nanoseconds, Integer(NodaConstants.NanosecondsPerSecond), typeof(long), _longTypeMapping);
            switch (columnStorage)
            {
                case DurationStorage.Nanoseconds:
                    return nanoseconds;

                case DurationStorage.Seconds:
                    return new SqlUnaryExpression(ExpressionType.Convert, wholeSeconds, typeof(Duration), columnTypeMapping);

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
                        _sqlExpressionFactory.Convert(wholeSeconds, typeof(int), _intTypeMapping));
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
                        argumentsPropagateNullability: _dateAddPropagatesNullability,
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
                        argumentsPropagateNullability: _dateAddPropagatesNullability,
                        typeof(Duration),
                        columnTypeMapping);
            }
        }

        /// <summary>
        /// A COALESCE or CASE between Durations (from ?? or ?:). When its branches are stored differently, they are all
        /// converted to nanoseconds, since SQL needs them the same; otherwise it is left as it is.
        /// </summary>
        public SqlExpression SameStorage(SqlExpression expression, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            switch (expression)
            {
                case SqlFunctionExpression { Name: "COALESCE" } coalesce:
                    if (!NeedNanoseconds(coalesce.Arguments))
                    {
                        WarnIfTruncatedToSeconds(coalesce.Arguments, logger);
                        return expression;
                    }

                    return coalesce
                        .Update(coalesce.Instance, coalesce.Arguments.Select(ToNanoseconds).ToList())
                        .ApplyTypeMapping(DurationBigIntTypeMapping.Default);

                case CaseExpression caseExpression:
                    var results = caseExpression.WhenClauses.Select(c => c.Result).Append(caseExpression.ElseResult).Where(r => r != null).ToList();
                    if (!NeedNanoseconds(results))
                    {
                        WarnIfTruncatedToSeconds(results, logger);
                        return expression;
                    }

                    return caseExpression.Update(
                        caseExpression.Operand,
                        caseExpression.WhenClauses.Select(c => new CaseWhenClause(c.Test, ToNanoseconds(c.Result))).ToList(),
                        caseExpression.ElseResult == null ? null : ToNanoseconds(caseExpression.ElseResult));

                default:
                    return expression;
            }
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

        /// <summary>Whether the branches are in more than one storage, all of which can be converted to nanoseconds.</summary>
        private static bool NeedNanoseconds(IEnumerable<SqlExpression> branches)
        {
            var storages = branches.Where(b => b.TypeMapping != null).Select(StorageOf).Distinct().ToList();
            return storages.Count > 1 && !storages.Contains(DurationStorage.Other);
        }

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
