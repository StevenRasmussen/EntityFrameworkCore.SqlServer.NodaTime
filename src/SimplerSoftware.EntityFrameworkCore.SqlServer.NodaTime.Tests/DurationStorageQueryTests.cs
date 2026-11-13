using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// A Duration stored as time, bigint (nanoseconds), int (seconds) or datetime2 (offset from 1900-01-01) (#38).
    /// Every query must give the same answer as NodaTime does in .NET for the values read back from the database
    /// (time and datetime2 keep 100ns, int whole seconds).
    /// The rows are in <see cref="RacingContext.DurationStorageRows"/>; they include values of 24 hours or more,
    /// negative values and fractions of a second, which only bigint and int can hold.
    /// </summary>
    public class DurationStorageQueryTests : QueryTestBase
    {
        public DurationStorageQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        public static TheoryData<string> Columns => new(
            nameof(DurationStorageTypes.Time),
            nameof(DurationStorageTypes.BigInt),
            nameof(DurationStorageTypes.Int),
            nameof(DurationStorageTypes.DateTime2));

        // Components

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Days(string column) => AssertSameAsDotNet(column, d => d.Days);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Hours(string column) => AssertSameAsDotNet(column, d => d.Hours);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Minutes(string column) => AssertSameAsDotNet(column, d => d.Minutes);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Seconds(string column) => AssertSameAsDotNet(column, d => d.Seconds);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Milliseconds(string column) => AssertSameAsDotNet(column, d => d.Milliseconds);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task SubsecondNanoseconds(string column) => AssertSameAsDotNet(column, d => d.SubsecondNanoseconds);

        /// <summary>Microseconds() only exists for queries; .NET's answer is the sub-second part in whole microseconds.</summary>
        [Theory]
        [MemberData(nameof(Columns))]
        public Task Microseconds(string column) => AssertSameAsDotNet(column, d => d.Microseconds(), d => d.SubsecondNanoseconds / 1000);

        // PlusX: done in nanoseconds, so a time value no longer wraps around at 24 hours

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusHours_past_24_hours(string column) => AssertSameAsDotNet(column, d => d.PlusHours(30));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusHours_with_a_fraction(string column) => AssertSameAsDotNet(column, d => d.PlusHours(1.5));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusMinutes_negative(string column) => AssertSameAsDotNet(column, d => d.PlusMinutes(-90.25));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusSeconds_with_a_fraction(string column) => AssertSameAsDotNet(column, d => d.PlusSeconds(0.1234));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusMilliseconds(string column) => AssertSameAsDotNet(column, d => d.PlusMilliseconds(1500.5));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusMicroseconds(string column) => AssertSameAsDotNet(column, d => d.PlusMicroseconds(2.5));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusNanoseconds(string column) => AssertSameAsDotNet(column, d => d.PlusNanoseconds(150));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusHours_with_a_parameter(string column)
        {
            var hours = 26.75;
            return AssertSameAsDotNet(column, d => d.PlusHours(hours));
        }

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Days_of_PlusHours(string column) => AssertSameAsDotNet(column, d => d.PlusHours(30).Days);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Hours_of_PlusHours(string column) => AssertSameAsDotNet(column, d => d.PlusHours(30).Hours);

        // + and -

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Add_a_duration(string column) => AssertSameAsDotNet(column, d => d + Duration.FromHours(25));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Add_itself(string column) => AssertSameAsDotNet(column, d => d + d);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Subtract_a_parameter(string column)
        {
            var thirtyHours = Duration.FromHours(30);
            return AssertSameAsDotNet(column, d => d - thirtyHours);
        }

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Plus_and_Minus(string column) => AssertSameAsDotNet(column, d => d.Plus(Duration.FromMinutes(1)).Minus(Duration.FromHours(2)));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Negate(string column) => AssertSameAsDotNet(column, d => -d);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Multiply(string column) => AssertSameAsDotNet(column, d => d * 2);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Multiply_on_the_left(string column) => AssertSameAsDotNet(column, d => -2 * d);

        /// <summary>NodaTime truncates toward zero, as T-SQL does.</summary>
        [Theory]
        [MemberData(nameof(Columns))]
        public Task Divide(string column) => AssertSameAsDotNet(column, d => d / 7);

        // Comparisons and ordering

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Greater_than_a_constant(string column) => AssertWhereSameAsDotNet(column, d => d > Duration.FromMinutes(45));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Equal_to_a_constant(string column) => AssertWhereSameAsDotNet(column, d => d == Duration.FromHours(1));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task PlusHours_compared_with_a_parameter_past_24_hours(string column)
        {
            var twentySixHours = Duration.FromHours(26);
            return AssertWhereSameAsDotNet(column, d => d.PlusHours(20) > twentySixHours);
        }

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Negative_result_compared_with_zero(string column) => AssertWhereSameAsDotNet(column, d => d.PlusMinutes(-90) < Duration.Zero);

        [Theory]
        [MemberData(nameof(Columns))]
        public Task Sum_compared_with_24_hours(string column) => AssertWhereSameAsDotNet(column, d => d + d > Duration.FromHours(24));

        [Theory]
        [MemberData(nameof(Columns))]
        public async Task OrderBy_PlusHours(string column)
        {
            var key = Substitute<Duration>(column, d => d.PlusHours(23));
            var rows = await ReadRows(NotNull(column));

            var expected = rows.OrderBy(key.Compile()).ThenBy(r => r.Id).Select(r => r.Id).ToList();
            var actual = await this.Db.DurationStorageTypes.Where(NotNull(column)).OrderBy(key).ThenBy(r => r.Id).Select(r => r.Id).ToListAsync();

            Assert.Equal(expected, actual);
        }

        // EF.Functions.DateDiff*: the number of boundaries crossed, as SQL Server's DATEDIFF counts them

        [Theory]
        [MemberData(nameof(Columns))]
        public Task DateDiffHour(string column)
            => AssertSameAsDotNet(column, d => EF.Functions.DateDiffHour(Duration.FromMinutes(90), d), d => (int)DateDiff(Duration.FromMinutes(90), d, NodaConstants.NanosecondsPerHour));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task DateDiffMinute(string column)
            => AssertSameAsDotNet(column, d => EF.Functions.DateDiffMinute(d, Duration.FromSeconds(30)), d => (int)DateDiff(d, Duration.FromSeconds(30), NodaConstants.NanosecondsPerMinute));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task DateDiffBigSecond(string column)
            => AssertSameAsDotNet(column, d => EF.Functions.DateDiffBigSecond(Duration.FromSeconds(30), d), d => DateDiff(Duration.FromSeconds(30), d, NodaConstants.NanosecondsPerSecond));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task DateDiffBigMillisecond(string column)
            => AssertSameAsDotNet(column, d => EF.Functions.DateDiffBigMillisecond(Duration.FromSeconds(30), d), d => DateDiff(Duration.FromSeconds(30), d, NodaConstants.NanosecondsPerMillisecond));

        /// <summary>The constant is sent as nanoseconds, so against an int column it keeps its fraction of a second.</summary>
        [Theory]
        [MemberData(nameof(Columns))]
        public Task DateDiffBigMillisecond_with_a_fraction(string column)
            => AssertSameAsDotNet(column, d => EF.Functions.DateDiffBigMillisecond(Duration.FromMilliseconds(30_500), d), d => DateDiff(Duration.FromMilliseconds(30_500), d, NodaConstants.NanosecondsPerMillisecond));

        [Theory]
        [MemberData(nameof(Columns))]
        public Task DateDiffBigNanosecond(string column)
            => AssertSameAsDotNet(column, d => EF.Functions.DateDiffBigNanosecond(Duration.FromSeconds(30), d), d => DateDiff(Duration.FromSeconds(30), d, 1));

        // Two Durations stored differently: both are converted to nanoseconds

        [Fact]
        public Task Time_compared_with_bigint()
            => AssertSameAsDotNet(x => x.Time < x.BigInt, x => x.Id);

        [Fact]
        public Task Int_equal_to_bigint()
            => AssertSameAsDotNet(x => x.Int == x.BigInt, x => x.Id);

        [Fact]
        public Task Int_greater_than_bigint()
            => AssertSameAsDotNet(x => x.Int > x.BigInt, x => x.Id);

        [Fact]
        public Task Time_not_equal_to_int_with_nulls()
            => AssertSameAsDotNet(x => x.Time != x.Int, x => x.Id);

        /// <summary>This compared correctly before only while the result stayed under 24 hours.</summary>
        [Fact]
        public Task PlusHours_compared_with_the_same_time_column()
            => AssertSameAsDotNet(x => x.Time != null && x.Time.Value.PlusHours(1) > x.Time.Value, x => x.Id);

        [Fact]
        public Task PlusHours_compared_with_another_PlusHours()
            => AssertSameAsDotNet(x => x.Time != null && x.Time.Value.PlusHours(20) > x.Time.Value.PlusMinutes(90), x => x.Id);

        [Fact]
        public Task PlusMinutes_compared_with_an_int_column()
            => AssertSameAsDotNet(x => x.Time != null && x.Int != null && x.Time.Value.PlusMinutes(30) >= x.Int.Value, x => x.Id);

        [Fact]
        public Task Time_plus_bigint()
            => AssertSameAsDotNet(x => x.Time != null && x.BigInt != null, x => x.Time.Value + x.BigInt.Value);

        [Fact]
        public Task BigInt_minus_int()
            => AssertSameAsDotNet(x => x.BigInt != null && x.Int != null, x => x.BigInt.Value - x.Int.Value);

        [Fact]
        public Task Time_minus_int()
            => AssertSameAsDotNet(x => x.Time != null && x.Int != null, x => x.Time.Value.Minus(x.Int.Value));

        [Fact]
        public Task Nullable_sum()
            => AssertSameAsDotNet(x => true, x => x.Time + x.Int);

        [Fact]
        public Task DateDiffBigNanosecond_between_time_and_bigint()
            => AssertSameAsDotNet(
                x => x.Time != null && x.BigInt != null,
                x => EF.Functions.DateDiffBigNanosecond(x.Time, x.BigInt),
                x => DateDiff(x.Time.Value, x.BigInt.Value, 1));

        /// <summary>A time and a datetime2 compare as a datetime2: SQL Server puts the time on 1900-01-01.</summary>
        [Fact]
        public Task Time_compared_with_datetime2()
            => AssertSameAsDotNet(x => x.Time < x.DateTime2, x => x.Id);

        [Fact]
        public Task DateTime2_equal_to_int()
            => AssertSameAsDotNet(x => x.DateTime2 == x.Int, x => x.Id);

        [Fact]
        public Task DateTime2_greater_than_bigint()
            => AssertSameAsDotNet(x => x.DateTime2 > x.BigInt, x => x.Id);

        [Fact]
        public Task Coalesce_of_time_and_datetime2()
            => AssertSameAsDotNet(x => true, x => x.Time ?? x.DateTime2);

        [Fact]
        public Task DateTime2_minus_time()
            => AssertSameAsDotNet(x => x.DateTime2 != null && x.Time != null, x => x.DateTime2.Value - x.Time.Value);

        /// <summary>SQL Server's DATEDIFF takes a time and a datetime2 together, on the same 1900-01-01 base.</summary>
        [Fact]
        public Task DateDiffHour_between_time_and_datetime2()
            => AssertSameAsDotNet(
                x => x.Time != null && x.DateTime2 != null,
                x => EF.Functions.DateDiffHour(x.Time, x.DateTime2),
                x => (int)DateDiff(x.Time.Value, x.DateTime2.Value, NodaConstants.NanosecondsPerHour));

        [Fact]
        public Task DateDiffMinute_between_int_and_bigint()
            => AssertSameAsDotNet(
                x => x.Int != null && x.BigInt != null,
                x => EF.Functions.DateDiffMinute(x.Int, x.BigInt),
                x => (int)DateDiff(x.Int.Value, x.BigInt.Value, NodaConstants.NanosecondsPerMinute));

        /// <summary>
        /// Comparing with something only .NET can compute makes EF Core run the comparison in .NET, as a call to the
        /// method the operator was rewritten into. It must still give the operator's answer, including for nulls.
        /// </summary>
        [Fact]
        public async Task Comparison_run_in_dotnet_keeps_the_operators_null_handling()
        {
            var rows = await ReadRows(x => true);
            var expected = rows.Select(x => x.Time > x.BigInt).ToList();

            var actual = await this.Db.DurationStorageTypes.OrderBy(x => x.Id).Select(x => x.Time > OnlyInDotNet(x.BigInt)).ToListAsync();

            Assert.Equal(expected, actual);
            Assert.Contains(true, actual);
            Assert.Contains(false, actual);
        }

        private static Duration? OnlyInDotNet(Duration? duration) => duration;

        // ?? and ?: whose branches are stored differently: all are converted to nanoseconds

        [Fact]
        public Task Coalesce_with_PlusHours()
            => AssertSameAsDotNet(x => x.BigInt != null, x => x.Time ?? x.BigInt.Value.PlusHours(1));

        [Fact]
        public Task Conditional_with_PlusHours()
            => AssertSameAsDotNet(x => x.Time != null, x => x.Id % 2 == 0 ? x.Time.Value : x.Time.Value.PlusHours(30));

        [Fact]
        public Task Conditional_with_a_nullable_branch()
            => AssertSameAsDotNet(x => true, x => x.Int != null ? x.Int.Value + x.Int.Value : x.Time);

        [Fact]
        public Task Coalesce_of_int_and_bigint_columns()
            => AssertSameAsDotNet(x => true, x => x.Int ?? x.BigInt);

        [Fact]
        public Task Conditional_of_bigint_and_time_columns()
            => AssertSameAsDotNet(x => true, x => x.Id % 2 == 0 ? x.BigInt : x.Time);

        [Fact]
        public Task Equals_between_int_and_bigint()
            => AssertSameAsDotNet(x => x.Int != null && x.BigInt != null && x.Int.Value.Equals(x.BigInt.Value), x => x.Id);

        [Fact]
        public Task Equals_object_between_int_and_bigint()
            => AssertSameAsDotNet(x => x.Int != null && x.BigInt != null && x.Int.Value.Equals((object)x.BigInt.Value), x => x.Id);

        /// <summary>
        /// NodaTime rounds a duration times a double to whole nanoseconds, which float arithmetic in SQL wouldn't
        /// match, so this isn't translated: EF Core runs it in .NET.
        /// </summary>
        [Theory]
        [MemberData(nameof(Columns))]
        public async Task Multiply_by_a_double_runs_in_dotnet(string column)
        {
            var rows = await ReadRows(NotNull(column));
            var value = ValueOf(column);
            var expected = rows.Select(r => value(r) * 1.5).ToList();

            var actual = await this.Db.DurationStorageTypes.Where(NotNull(column)).OrderBy(x => x.Id).Select(Substitute<Duration>(column, d => d * 1.5)).ToListAsync();

            Assert.Equal(expected, actual);
        }

        // ExecuteUpdate converts the value to the column's storage

        /// <summary>x.Time.PlusHours(2) is computed in nanoseconds and written back into a time.</summary>
        [Fact]
        public Task ExecuteUpdate_time_with_PlusHours()
            => AssertExecuteUpdate(
                x => x.Time < Duration.FromHours(22),
                s => s.SetProperty(x => x.Time, x => x.Time.Value.PlusHours(2)),
                (before, after) => Assert.Equal(Iso(before.Time.Value + Duration.FromHours(2)), Iso(after.Time.Value)));

        /// <summary>
        /// A time can't hold 24 hours or more, so this fails, as saving the value would. DATEADD on a time used to wrap
        /// it around to 01:00 instead.
        /// </summary>
        [Fact]
        public async Task ExecuteUpdate_time_past_24_hours_fails()
        {
            var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => AssertExecuteUpdate(
                x => x.Id == 8,
                s => s.SetProperty(x => x.Time, x => x.Time.Value.PlusHours(2)),
                (before, after) => { }));

            Assert.Contains("cannot be stored in a time column", exception.Message);
        }

        [Fact]
        public Task ExecuteUpdate_int_with_PlusSeconds()
            => AssertExecuteUpdate(
                x => x.Int != null,
                s => s.SetProperty(x => x.Int, x => x.Int.Value.PlusSeconds(1.5)),
                (before, after) => Assert.Equal(Iso(WholeSeconds(before.Int.Value + Duration.FromMilliseconds(1500))), Iso(after.Int.Value)));

        [Fact]
        public Task ExecuteUpdate_bigint_from_an_int_column()
            => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.BigInt, x => x.Int),
                (before, after) => Assert.Equal(before.Int, after.BigInt));

        [Fact]
        public Task ExecuteUpdate_int_from_a_time_column()
            => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.Int, x => x.Time),
                (before, after) => Assert.Equal(before.Time.HasValue ? WholeSeconds(before.Time.Value) : null, after.Int));

        [Fact]
        public Task ExecuteUpdate_time_from_a_bigint_column()
            => AssertExecuteUpdate(
                x => x.BigInt >= Duration.Zero && x.BigInt < Duration.FromDays(1),
                s => s.SetProperty(x => x.Time, x => x.BigInt),
                (before, after) => Assert.Equal(Iso(Duration.FromTicks(before.BigInt.Value.BclCompatibleTicks)), Iso(after.Time.Value)));

        [Fact]
        public Task ExecuteUpdate_datetime2_with_PlusHours()
            => AssertExecuteUpdate(
                x => x.DateTime2 != null,
                s => s.SetProperty(x => x.DateTime2, x => x.DateTime2.Value.PlusHours(-30)),
                (before, after) => Assert.Equal(Iso(Ticks(before.DateTime2.Value - Duration.FromHours(30))), Iso(after.DateTime2.Value)));

        [Fact]
        public Task ExecuteUpdate_datetime2_from_a_time_column()
            => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.DateTime2, x => x.Time),
                (before, after) => Assert.Equal(before.Time, after.DateTime2));

        [Fact]
        public Task ExecuteUpdate_datetime2_from_a_bigint_column()
            => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.DateTime2, x => x.BigInt),
                (before, after) => Assert.Equal(before.BigInt.HasValue ? Ticks(before.BigInt.Value) : null, after.DateTime2));

        [Fact]
        public Task ExecuteUpdate_datetime2_from_an_int_column()
            => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.DateTime2, x => x.Int),
                (before, after) => Assert.Equal(before.Int, after.DateTime2));

        [Fact]
        public Task ExecuteUpdate_bigint_from_a_datetime2_column()
            => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.BigInt, x => x.DateTime2),
                (before, after) => Assert.Equal(before.DateTime2, after.BigInt));

        /// <summary>
        /// datetime2 holds about -1899 to +8099 years, far beyond a bigint of nanoseconds (about ±292 years).
        /// Storing, comparing and the components work for all of it; arithmetic, being done in nanoseconds, doesn't.
        /// </summary>
        [Fact]
        public async Task DateTime2_holds_durations_of_a_thousand_years()
        {
            var options = new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer(this.SqlConnection.ConnectionString, x => Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsBuilderExtensions.UseNodaTime(x))
                .Options;
            using var db = new RacingContext(options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var long1 = Duration.FromDays(1000 * 365) + Duration.FromHours(5) + Duration.FromTicks(123);
            var long2 = -(Duration.FromDays(1500 * 365) + Duration.FromMinutes(7) + Duration.FromTicks(45));
            var added = new[] { new DurationStorageTypes { DateTime2 = long1, Int = Duration.FromSeconds(5) }, new DurationStorageTypes { DateTime2 = long2, Int = Duration.FromSeconds(5) } };
            db.DurationStorageTypes.AddRange(added);
            await db.SaveChangesAsync();
            var ids = added.Select(x => x.Id).ToList();
            var threshold = Duration.FromDays(900 * 365);

            var rows = await db.DurationStorageTypes.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.DateTime2,
                    x.DateTime2.Value.Days,
                    x.DateTime2.Value.Hours,
                    x.DateTime2.Value.Minutes,
                    x.DateTime2.Value.SubsecondNanoseconds,
                    Long = x.DateTime2 > threshold,
                    // An int is converted to a datetime2 to compare, which can't overflow.
                    LongerThanInt = x.DateTime2 > x.Int,
                })
                .ToListAsync();

            Assert.Equal(
                new[]
                {
                    (long1, long1.Days, long1.Hours, long1.Minutes, long1.SubsecondNanoseconds, true, true),
                    (long2, long2.Days, long2.Hours, long2.Minutes, long2.SubsecondNanoseconds, false, false),
                },
                rows.Select(r => (r.DateTime2.Value, r.Days, r.Hours, r.Minutes, r.SubsecondNanoseconds, r.Long, r.LongerThanInt)).ToArray());
        }

        // Text: equality only

        [Fact]
        public async Task Text_keeps_every_nanosecond()
        {
            var rows = await ReadRows(x => true);

            Assert.Equal(rows.Select(x => x.BigInt).ToList(), rows.Select(x => x.Text).ToList());
        }

        [Fact]
        public Task Text_equal_to_a_constant()
            => AssertSameAsDotNet(x => x.Text == Duration.FromHours(25), x => x.Id);

        [Fact]
        public Task Text_equal_to_a_negative_parameter()
        {
            var value = -(Duration.FromHours(26) + Duration.FromMinutes(3) + Duration.FromMilliseconds(500));
            return AssertSameAsDotNet(x => x.Text == value, x => x.Id);
        }

        [Fact]
        public Task Text_not_equal_with_nulls()
        {
            var value = Duration.FromHours(1);
            return AssertSameAsDotNet(x => x.Text != value, x => x.Id);
        }

        [Fact]
        public Task Text_Equals()
            => AssertSameAsDotNet(x => x.Text != null && x.Text.Value.Equals(Duration.FromHours(1)), x => x.Id);

        /// <summary>Every value has the same width, so text order is duration order for values that aren't negative.</summary>
        [Fact]
        public async Task Text_sorts_values_that_arent_negative()
        {
            var rows = await ReadRows(x => x.BigInt >= Duration.Zero);
            var expected = rows.OrderBy(x => x.BigInt).Select(x => x.Id).ToList();

            var actual = await this.Db.DurationStorageTypes.Where(x => x.BigInt >= Duration.Zero).OrderBy(x => x.Text).Select(x => x.Id).ToListAsync();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public Task ExecuteUpdate_text_with_a_parameter()
        {
            var value = -Duration.FromNanoseconds(123_456_789_012);
            return AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.Text, value),
                (before, after) => Assert.Equal(value, after.Text));
        }

        /// <summary>Text can't be converted to or from another storage in SQL, so this isn't translated.</summary>
        [Fact]
        public async Task ExecuteUpdate_text_from_a_bigint_column_is_not_translated()
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => AssertExecuteUpdate(
                x => true,
                s => s.SetProperty(x => x.Text, x => x.BigInt),
                (before, after) => { }));

            Assert.Contains("could not be translated", exception.Message);
        }

        /// <summary>Runs ExecuteUpdate in a transaction that is rolled back, and checks each updated row.</summary>
        private async Task AssertExecuteUpdate(
            Expression<Func<DurationStorageTypes, bool>> filter,
            Action<UpdateSettersBuilder<DurationStorageTypes>> setters,
            Action<DurationStorageTypes, DurationStorageTypes> assertRow)
        {
            var options = new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer(this.SqlConnection.ConnectionString, x => Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsBuilderExtensions.UseNodaTime(x))
                .Options;
            using var db = new RacingContext(options);
            using var transaction = await db.Database.BeginTransactionAsync();

            var before = await db.DurationStorageTypes.AsNoTracking().Where(filter).OrderBy(x => x.Id).ToListAsync();
            var updated = await db.DurationStorageTypes.Where(filter).ExecuteUpdateAsync(setters);
            var ids = before.Select(x => x.Id).ToList();
            var after = await db.DurationStorageTypes.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();

            Assert.NotEmpty(before);
            Assert.Equal(before.Count, updated);
            foreach (var (row, updatedRow) in before.Zip(after))
                assertRow(row, updatedRow);
        }

        /// <summary>What a time or datetime2 keeps: whole ticks (100ns), truncated toward zero.</summary>
        private static Duration Ticks(Duration duration) => Duration.FromTicks(duration.BclCompatibleTicks);

        private static Duration WholeSeconds(Duration duration)
            => Duration.FromSeconds(duration.ToInt64Nanoseconds() / NodaConstants.NanosecondsPerSecond);

        // Helpers

        /// <summary>DATEDIFF's count: floor(end / unit) - floor(start / unit).</summary>
        private static long DateDiff(Duration start, Duration end, long nanosecondsPerUnit)
        {
            static long FloorDivide(long value, long divisor) => value / divisor - (value % divisor < 0 ? 1 : 0);
            return FloorDivide(end.ToInt64Nanoseconds(), nanosecondsPerUnit) - FloorDivide(start.ToInt64Nanoseconds(), nanosecondsPerUnit);
        }

        /// <summary>Runs <paramref name="query"/> on the column's non-null values in SQL and in .NET, and compares.</summary>
        private Task AssertSameAsDotNet<T>(string column, Expression<Func<Duration, T>> query, Func<Duration, T> expected = null)
        {
            var selector = Substitute(column, query);
            var value = ValueOf(column);
            var dotNet = expected ?? query.Compile();
            return AssertSameAsDotNet(NotNull(column), selector, row => dotNet(value(row)));
        }

        private Task AssertWhereSameAsDotNet(string column, Expression<Func<Duration, bool>> predicate)
        {
            var filter = Substitute(column, predicate);
            var notNull = NotNull(column);
            var notNullBody = new ReplaceParameter(notNull.Parameters[0], filter.Parameters[0]).Visit(notNull.Body);
            var both = Expression.Lambda<Func<DurationStorageTypes, bool>>(Expression.AndAlso(notNullBody, filter.Body), filter.Parameters);
            return AssertSameAsDotNet(both, x => x.Id);
        }

        private async Task AssertSameAsDotNet<T>(
            Expression<Func<DurationStorageTypes, bool>> filter,
            Expression<Func<DurationStorageTypes, T>> selector,
            Func<DurationStorageTypes, T> expected = null)
        {
            var rows = await ReadRows(filter);
            var compute = expected ?? selector.Compile();
            var expectedValues = rows.Select(row => $"{row.Id}: {Format(compute(row))}").ToList();

            var actual = await this.Db.DurationStorageTypes.Where(filter).OrderBy(x => x.Id).Select(selector).ToListAsync();
            var actualValues = rows.Zip(actual, (row, value) => $"{row.Id}: {Format(value)}").ToList();

            Assert.Equal(rows.Count, actual.Count);
            Assert.Equal(expectedValues, actualValues);
            AssertNotEvaluatedOnClient();
        }

        /// <summary>
        /// EF Core runs a final projection it can't translate in .NET instead, which would make the test pass
        /// without testing any SQL. Then the SELECT list is just columns.
        /// </summary>
        private void AssertNotEvaluatedOnClient()
        {
            var sql = condense(this.Db.Sql);
            var selectList = sql.Substring("SELECT ".Length, sql.IndexOf(" FROM ", StringComparison.Ordinal) - "SELECT ".Length);
            var isOnlyId = selectList == "[d].[Id]";
            Assert.True(isOnlyId || !Regex.IsMatch(selectList, @"^\[d\]\.\[\w+\](, \[d\]\.\[\w+\])*$"), $"The projection was evaluated on the client: {sql}");
        }

        private async Task<List<DurationStorageTypes>> ReadRows(Expression<Func<DurationStorageTypes, bool>> filter)
        {
            // Filtering in .NET keeps the expected rows independent of the SQL being tested.
            var all = await this.Db.DurationStorageTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            return all.Where(filter.Compile()).ToList();
        }

        private static string Format(object value) => value is Duration duration ? Iso(duration) : value?.ToString() ?? "null";

        /// <summary>x => x.Column.Value</summary>
        private static Func<DurationStorageTypes, Duration> ValueOf(string column)
        {
            var property = typeof(DurationStorageTypes).GetProperty(column);
            return row => ((Duration?)property.GetValue(row)).Value;
        }

        /// <summary>x => x.Column != null</summary>
        private static Expression<Func<DurationStorageTypes, bool>> NotNull(string column)
        {
            var x = Expression.Parameter(typeof(DurationStorageTypes), "x");
            return Expression.Lambda<Func<DurationStorageTypes, bool>>(
                Expression.NotEqual(Expression.Property(x, column), Expression.Constant(null, typeof(Duration?))), x);
        }

        /// <summary>d => f(d) becomes x => f(x.Column.Value)</summary>
        private static Expression<Func<DurationStorageTypes, T>> Substitute<T>(string column, Expression<Func<Duration, T>> query)
        {
            var x = Expression.Parameter(typeof(DurationStorageTypes), "x");
            var value = Expression.Property(Expression.Property(x, column), nameof(Nullable<Duration>.Value));
            var body = new ReplaceParameter(query.Parameters[0], value).Visit(query.Body);
            return Expression.Lambda<Func<DurationStorageTypes, T>>(body, x);
        }

        private sealed class ReplaceParameter(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : node;
        }
    }
}
