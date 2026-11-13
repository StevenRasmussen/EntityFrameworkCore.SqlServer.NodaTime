using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
    /// Period properties (#3), stored as JSON. Every query must give the same answer as NodaTime does in .NET for the values
    /// read back. The rows are in <see cref="RacingContext.PeriodEventRows"/>.
    /// </summary>
    public class PeriodQueryTests : QueryTestBase
    {
        public PeriodQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        [Fact]
        public async Task Rows_round_trip()
        {
            var rows = await this.Db.PeriodEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

            Assert.Equal(RacingContext.PeriodEventRows().Select(Format), rows.Select(Format));
        }

        [Fact]
        public void Stored_as_json()
        {
            var sql = this.Db.PeriodEvents.Select(x => x.Every.Days).ToQueryString();

            Assert.Contains("JSON_VALUE([p].[Every], '$.Days')", sql);
        }

        public static TheoryData<string> Selectors => new(SelectorExpressions.Keys);

        private static readonly Dictionary<string, LambdaExpression> SelectorExpressions = new()
        {
            // The period and its parts
            ["Every"] = (Expression<Func<PeriodEvent, Period>>)(x => x.Every),
            ["Grace"] = (Expression<Func<PeriodEvent, Period>>)(x => x.Grace),
            ["Rule.Every"] = (Expression<Func<PeriodEvent, Period>>)(x => x.Rule.Every),
            ["Every.Years"] = (Expression<Func<PeriodEvent, int>>)(x => x.Every.Years),
            ["Every.Months"] = (Expression<Func<PeriodEvent, int>>)(x => x.Every.Months),
            ["Every.Days"] = (Expression<Func<PeriodEvent, int>>)(x => x.Every.Days),
            ["Every.Hours"] = (Expression<Func<PeriodEvent, long>>)(x => x.Every.Hours),
            ["Every.Minutes"] = (Expression<Func<PeriodEvent, long>>)(x => x.Every.Minutes),
            ["Every.Ticks"] = (Expression<Func<PeriodEvent, long>>)(x => x.Every.Ticks),
            ["Every.Nanoseconds"] = (Expression<Func<PeriodEvent, long>>)(x => x.Every.Nanoseconds),
            ["Every.HasDateComponent"] = (Expression<Func<PeriodEvent, bool>>)(x => x.Every.HasDateComponent),
            ["Every.HasTimeComponent"] = (Expression<Func<PeriodEvent, bool>>)(x => x.Every.HasTimeComponent),
            ["Rule.Every.HasTimeComponent"] = (Expression<Func<PeriodEvent, bool>>)(x => x.Rule.Every != null && x.Rule.Every.HasTimeComponent),

            // Adding a period to a LocalDateTime
            ["From + Every"] = (Expression<Func<PeriodEvent, LocalDateTime>>)(x => x.From + x.Every),
            ["From - Every"] = (Expression<Func<PeriodEvent, LocalDateTime>>)(x => x.From - x.Every),
            ["From.Plus(Every)"] = (Expression<Func<PeriodEvent, LocalDateTime>>)(x => x.From.Plus(x.Every)),
            ["From.Minus(Every)"] = (Expression<Func<PeriodEvent, LocalDateTime>>)(x => x.From.Minus(x.Every)),
            ["LocalDateTime.Add(From, Every)"] = (Expression<Func<PeriodEvent, LocalDateTime>>)(x => LocalDateTime.Add(x.From, x.Every)),
            ["LocalDateTime.Subtract(From, Every)"] = (Expression<Func<PeriodEvent, LocalDateTime>>)(x => LocalDateTime.Subtract(x.From, x.Every)),
            ["From + Rule.Every"] = (Expression<Func<PeriodEvent, LocalDateTime?>>)(x => x.Rule.Every == null ? null : x.From + x.Rule.Every),
            ["From + Shift"] = (Expression<Func<PeriodEvent, LocalDateTime?>>)(x => x.Shift == null ? null : x.From + x.Shift),

            // Adding a period to a LocalDate: date units only
            ["FromDate + Grace"] = (Expression<Func<PeriodEvent, LocalDate?>>)(x => x.Grace == null ? null : x.FromDate + x.Grace),
            ["FromDate - Grace"] = (Expression<Func<PeriodEvent, LocalDate?>>)(x => x.Grace == null ? null : x.FromDate - x.Grace),
            ["FromDate.Plus(Grace)"] = (Expression<Func<PeriodEvent, LocalDate?>>)(x => x.Grace == null ? null : x.FromDate.Plus(x.Grace)),

            // Adding a period to a LocalTime: time units only, wrapping around midnight
            ["At + Shift"] = (Expression<Func<PeriodEvent, LocalTime?>>)(x => x.Shift == null ? null : x.At + x.Shift),
            ["At - Shift"] = (Expression<Func<PeriodEvent, LocalTime?>>)(x => x.Shift == null ? null : x.At - x.Shift),
            ["At.Minus(Shift)"] = (Expression<Func<PeriodEvent, LocalTime?>>)(x => x.Shift == null ? null : x.At.Minus(x.Shift)),

            // ToDuration: no years or months
            ["Shift.ToDuration()"] = (Expression<Func<PeriodEvent, Duration?>>)(x => x.Shift == null ? null : x.Shift.ToDuration()),
            ["Grace.ToDuration()"] = (Expression<Func<PeriodEvent, Duration?>>)(x => x.Grace == null ? null : x.Grace.ToDuration()),
            ["Every.ToDuration() without months"] = (Expression<Func<PeriodEvent, Duration?>>)(x => x.Every.Years == 0 && x.Every.Months == 0 ? x.Every.ToDuration() : null),

            // One unit between two values
            ["Years between dates"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.FromDate, x.UntilDate, PeriodUnits.Years).Years),
            ["Months between dates"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.FromDate, x.UntilDate, PeriodUnits.Months).Months),
            ["Weeks between dates"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.FromDate, x.UntilDate, PeriodUnits.Weeks).Weeks),
            ["Days between dates"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.FromDate, x.UntilDate, PeriodUnits.Days).Days),
            ["Period.DaysBetween"] = (Expression<Func<PeriodEvent, int>>)(x => Period.DaysBetween(x.FromDate, x.UntilDate)),
            ["Years between date and times"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Years).Years),
            ["Months between date and times"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Months).Months),
            ["Weeks between date and times"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Weeks).Weeks),
            ["Days between date and times"] = (Expression<Func<PeriodEvent, int>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Days).Days),
            ["Hours between date and times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Hours).Hours),
            ["Minutes between date and times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Minutes).Minutes),
            ["Seconds between date and times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Seconds).Seconds),
            ["Milliseconds between date and times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Milliseconds).Milliseconds),
            ["Ticks between date and times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Ticks).Ticks),
            ["Nanoseconds between date and times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.From, x.Until, PeriodUnits.Nanoseconds).Nanoseconds),
            ["Hours between times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.At, x.UntilTime, PeriodUnits.Hours).Hours),
            ["Minutes between times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.At, x.UntilTime, PeriodUnits.Minutes).Minutes),
            ["Ticks between times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.At, x.UntilTime, PeriodUnits.Ticks).Ticks),
            ["Nanoseconds between times"] = (Expression<Func<PeriodEvent, long>>)(x => Period.Between(x.At, x.UntilTime, PeriodUnits.Nanoseconds).Nanoseconds),
        };

        [Theory]
        [MemberData(nameof(Selectors))]
        public Task Select(string selector)
            => (Task)typeof(PeriodQueryTests)
                .GetMethod(nameof(AssertSelectSameAsDotNet), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(SelectorExpressions[selector].ReturnType)
                .Invoke(this, [SelectorExpressions[selector]])!;

        // Filters

        [Fact]
        public Task Equal_to_a_parameter()
        {
            var period = RacingContext.PeriodEventRows()[1].Every;
            return AssertWhereSameAsDotNet(x => x.Every == period);
        }

        [Fact]
        public Task Not_equal_to_a_parameter()
        {
            var period = Period.FromHours(36);
            return AssertWhereSameAsDotNet(x => x.Every != period);
        }

        /// <summary>As NodaTime compares periods: unit by unit, so 36 hours isn't a day and a half.</summary>
        [Fact]
        public Task Equal_units_only()
        {
            var period = Period.FromDays(1) + Period.FromHours(12);
            return AssertWhereSameAsDotNet(x => x.Every == period);
        }

        [Fact]
        public Task Null() => AssertWhereSameAsDotNet(x => x.Grace == null);

        [Fact]
        public Task Equal_to_another_column() => AssertWhereSameAsDotNet(x => x.Shift == x.Grace);

        [Fact]
        public Task HasTimeComponent() => AssertWhereSameAsDotNet(x => x.Every.HasTimeComponent);

        [Fact]
        public Task Unit() => AssertWhereSameAsDotNet(x => x.Every.Days < 0 || x.Every.Minutes > 60);

        /// <summary>
        /// A null period has no date or time units, rather than NULL ones, which EF Core would make count as not zero, as
        /// null != 0 is in C#. (In .NET, a null period has no HasDateComponent to read.)
        /// </summary>
        [Fact]
        public async Task HasComponent_of_a_null_period()
        {
            var withDate = await this.Db.PeriodEvents.Where(x => x.Grace.HasDateComponent).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
            var withoutTime = await this.Db.PeriodEvents.Where(x => !x.Grace.HasTimeComponent).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

            Assert.Equal([2, 3], withDate);
            Assert.Equal([1, 2, 3, 4], withoutTime);
        }

        /// <summary>
        /// Time units that would overflow a bigint as nanoseconds: 3,000,000 hours is about 342 years, 10,000,000,000
        /// seconds about 317.
        /// </summary>
        [Fact]
        public async Task Large_time_units()
        {
            var hours = Period.FromHours(3_000_000);
            var rows = await this.Db.PeriodEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

            Assert.Equal(
                rows.Select(x => Iso(x.From + hours)),
                (await this.Db.PeriodEvents.OrderBy(x => x.Id).Select(x => x.From + hours).ToListAsync()).Select(v => Iso(v)));
            Assert.Equal(
                rows.Select(x => Iso(x.From.Plus(Period.FromSeconds(10_000_000_000L)))),
                (await this.Db.PeriodEvents.OrderBy(x => x.Id).Select(x => x.From.Plus(Period.FromSeconds(10_000_000_000L))).ToListAsync()).Select(v => Iso(v)));
            Assert.Equal(
                rows.Select(x => Iso(x.At + Period.FromSeconds(10_000_000_000L))),
                (await this.Db.PeriodEvents.OrderBy(x => x.Id).Select(x => x.At + Period.FromSeconds(10_000_000_000L)).ToListAsync()).Select(v => Iso(v)));
        }

        [Fact]
        public Task Added_to_a_date_and_time()
        {
            var cutoff = new LocalDateTime(2025, 1, 1, 0, 0);
            return AssertWhereSameAsDotNet(x => x.From + x.Every < cutoff);
        }

        [Fact]
        public Task Added_to_a_date()
        {
            var cutoff = new LocalDate(2024, 2, 10);
            return AssertWhereSameAsDotNet(x => x.Grace != null && x.FromDate.Plus(x.Grace) > cutoff);
        }

        [Fact]
        public Task ToDuration_compared() => AssertWhereSameAsDotNet(x => x.Grace != null && x.Grace.ToDuration() > Duration.FromDays(2));

        [Fact]
        public Task ToDuration_in_Duration_arithmetic()
        {
            var day = Duration.FromDays(1);
            return AssertWhereSameAsDotNet(x => x.Shift != null && x.Shift.ToDuration() + day > Duration.FromHours(20));
        }

        /// <summary>An age: whole years from the date until today.</summary>
        [Fact]
        public Task Years_until_today()
        {
            var today = new LocalDate(2025, 3, 27);
            return AssertWhereSameAsDotNet(x => Period.Between(x.FromDate, today, PeriodUnits.Years).Years >= 1);
        }

        [Fact]
        public Task Added_period_is_a_parameter()
        {
            var period = Period.FromMonths(1) + Period.FromDays(1) + Period.FromHours(1);
            var cutoff = new LocalDateTime(2024, 4, 1, 0, 0);
            return AssertWhereSameAsDotNet(x => x.From + period < cutoff);
        }

        [Fact]
        public Task Added_period_is_a_constant()
            => AssertWhereSameAsDotNet(x => x.From.Plus(Period.FromWeeks(1)) > new LocalDateTime(2024, 3, 7, 0, 0));

        [Fact]
        public async Task OrderBy_a_unit()
        {
            var rows = await this.Db.PeriodEvents.AsNoTracking().ToListAsync();

            Assert.Equal(
                rows.OrderBy(x => x.Every.Hours).ThenBy(x => x.Id).Select(x => x.Id),
                await this.Db.PeriodEvents.OrderBy(x => x.Every.Hours).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync());
        }

        // Where NodaTime throws, so does the SQL, rather than give a different answer.

        [Fact]
        public async Task Time_units_added_to_a_date_fail()
        {
            Assert.Throws<ArgumentException>(() => RacingContext.PeriodEventRows()[1].FromDate + RacingContext.PeriodEventRows()[1].Every);

            var exception = await Assert.ThrowsAsync<SqlException>(() => this.Db.PeriodEvents.Where(x => x.Id == 2).Select(x => x.FromDate + x.Every).ToListAsync());
            Assert.Contains("A Period with time units can't be added to a LocalDate.", exception.Message);
        }

        [Fact]
        public async Task Date_units_added_to_a_time_fail()
        {
            Assert.Throws<ArgumentException>(() => RacingContext.PeriodEventRows()[0].At + RacingContext.PeriodEventRows()[0].Every);

            var exception = await Assert.ThrowsAsync<SqlException>(() => this.Db.PeriodEvents.Where(x => x.Id == 1).Select(x => x.At + x.Every).ToListAsync());
            Assert.Contains("A Period with date units can't be added to a LocalTime.", exception.Message);
        }

        [Fact]
        public async Task ToDuration_with_months_fails()
        {
            Assert.Throws<InvalidOperationException>(() => RacingContext.PeriodEventRows()[0].Every.ToDuration());

            var exception = await Assert.ThrowsAsync<SqlException>(() => this.Db.PeriodEvents.Where(x => x.Id == 1).Select(x => x.Every.ToDuration()).ToListAsync());
            Assert.Contains("A Period with years or months has no fixed Duration.", exception.Message);
        }

        // Writing

        [Fact]
        public async Task Update()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime()).Options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var row = await db.PeriodEvents.OrderBy(x => x.Id).FirstAsync();
            row.Every = new PeriodBuilder { Weeks = -2, Milliseconds = 1500 }.Build();
            row.Grace = Period.FromDays(10);
            row.Rule.Every = null;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.PeriodEvents.SingleAsync(x => x.Id == row.Id);

            Assert.Equal(Format(row), Format(readBack));
        }

        [Fact]
        public async Task ExecuteUpdate()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime()).Options);
            using var transaction = await db.Database.BeginTransactionAsync();

            await db.PeriodEvents.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.Grace, Period.FromHours(5)));
            await db.PeriodEvents.Where(x => x.Id == 2).ExecuteUpdateAsync(s => s.SetProperty(x => x.Shift, x => x.Every));

            Assert.Equal(Period.FromHours(5), (await db.PeriodEvents.AsNoTracking().SingleAsync(x => x.Id == 1)).Grace);
            Assert.Equal(RacingContext.PeriodEventRows()[1].Every, (await db.PeriodEvents.AsNoTracking().SingleAsync(x => x.Id == 2)).Shift);
        }

        /// <summary>In a JSON column already, a Period is a JSON object there.</summary>
        [Fact]
        public async Task In_a_json_column()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime()).Options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var every = new PeriodBuilder { Months = 2, Hours = -3 }.Build();
            var row = new NodaTimeCollections { Json = new NodaTimeJson { Every = every } };
            db.NodaTimeCollections.Add(row);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.NodaTimeCollections.SingleAsync(x => x.Id == row.Id);
            var hours = await db.NodaTimeCollections.Where(x => x.Id == row.Id).Select(x => x.Json.Every.Hours).SingleAsync();
            var sql = "SELECT JSON_VALUE([Json], '$.Every.Months') AS [Value] FROM [NodaTimeCollections] WHERE [Id] = " + row.Id;

            Assert.Equal(every, readBack.Json.Every);
            Assert.Equal(-3, hours);
            Assert.Equal("2", await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }

        // Helpers

        private static string Format(object value) => value switch
        {
            null => "null",
            PeriodEvent row => $"{Format(row.Every)} | {Format(row.Grace)} | {Format(row.Shift)} | {Format(row.From)} | {Format(row.Until)} | {Format(row.FromDate)} | {Format(row.UntilDate)} | {Format(row.At)} | {Format(row.UntilTime)} | {row.Rule.Name} {Format(row.Rule.Every)}",
            Period period => period.ToString(),
            Instant or LocalDateTime or OffsetDateTime or LocalDate or LocalTime or Duration => Iso(value),
            _ => value.ToString(),
        };

        private async Task AssertSelectSameAsDotNet<T>(Expression<Func<PeriodEvent, T>> selector)
        {
            var rows = await this.Db.PeriodEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            var compute = selector.Compile();
            var expected = rows.Select(row => $"{row.Id}: {Format(compute(row))}").ToList();

            var actual = await this.Db.PeriodEvents.OrderBy(x => x.Id).Select(selector).ToListAsync();
            var actualValues = rows.Zip(actual, (row, value) => $"{row.Id}: {Format(value)}").ToList();

            Assert.Equal(expected, actualValues);

            // EF Core runs a final projection it can't translate in .NET instead, which would test no SQL: then the SELECT
            // list is just the columns it needs. A whole Period really is just its column, though.
            var sql = condense(this.Db.Sql);
            var selectList = sql.Substring("SELECT ".Length, sql.IndexOf(" FROM ", StringComparison.Ordinal) - "SELECT ".Length);
            var justColumns = Regex.IsMatch(selectList, @"^\[p\]\.\[\w+\](, \[p\]\.\[\w+\])*$");
            Assert.True(!justColumns || typeof(T) == typeof(Period), $"The projection was evaluated on the client: {sql}");
        }

        private async Task AssertWhereSameAsDotNet(Expression<Func<PeriodEvent, bool>> predicate)
        {
            var rows = await this.Db.PeriodEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            var expected = rows.Where(predicate.Compile()).Select(x => x.Id).ToList();

            var actual = await this.Db.PeriodEvents.Where(predicate).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

            Assert.Equal(expected, actual);
        }
    }
}
