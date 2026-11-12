using Microsoft.EntityFrameworkCore;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Combining a LocalDate and a LocalTime into a LocalDateTime in SQL (date.At(time), time.On(date), date + time,
    /// date.AtMidnight()) and the reverse (TimeOfDay)
    /// (https://github.com/StevenRasmussen/EntityFrameworkCore.SqlServer.NodaTime/issues/48).
    /// Every seeded Race has ScheduledStart == Date.At(ScheduledStartTime): race n is on 2019-n-01.
    /// </summary>
    public class LocalDateTimeCombineQueryTests : QueryTestBase
    {
        public LocalDateTimeCombineQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture) { }

        private const string Combined = "DATEADD(day, DATEDIFF(day, '19000101', [r].[Date]), CAST([r].[ScheduledStartTime] AS datetime2))";

        // ---- The three ways of writing it ----

        [Fact]
        public async Task LocalDate_At_LocalTime()
        {
            var races = await this.Db.Race.Where(r => r.Date.At(r.ScheduledStartTime) > new LocalDateTime(2019, 6, 1, 0, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE {Combined} > '2019-06-01T00:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(7, races.Count);
        }

        [Fact]
        public async Task LocalTime_On_LocalDate()
        {
            var races = await this.Db.Race.Where(r => r.ScheduledStartTime.On(r.Date) > new LocalDateTime(2019, 6, 1, 0, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE {Combined} > '2019-06-01T00:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(7, races.Count);
        }

        [Fact]
        public async Task LocalDate_plus_LocalTime()
        {
            var races = await this.Db.Race.Where(r => r.Date + r.ScheduledStartTime > new LocalDateTime(2019, 6, 1, 0, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE {Combined} > '2019-06-01T00:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(7, races.Count);
        }

        // ---- The value is right ----

        [Fact]
        public async Task Combined_value_equals_the_stored_LocalDateTime()
        {
            var races = await this.Db.Race.Where(r => r.Date.At(r.ScheduledStartTime) == r.ScheduledStart).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE {Combined} = [r].[ScheduledStart]"),
                condense(this.Db.Sql));

            Assert.Equal(12, races.Count);
        }

        [Fact]
        public async Task Combined_value_can_be_selected()
        {
            var rows = await this.Db.Race
                .OrderBy(r => r.Id)
                .Select(r => new { r.ScheduledStart, Combined = r.Date + r.ScheduledStartTime })
                .ToListAsync();

            Assert.Equal(
                condense($"SELECT [r].[ScheduledStart], {Combined} AS [Combined] FROM [Race] AS [r] ORDER BY [r].[Id]"),
                condense(this.Db.Sql));

            Assert.Equal(12, rows.Count);
            Assert.All(rows, row => Assert.Equal(Iso(row.ScheduledStart), Iso(row.Combined)));
        }

        [Fact]
        public async Task Combined_value_can_be_ordered_by()
        {
            var ids = await this.Db.Race.OrderByDescending(r => r.Date.At(r.ScheduledStartTime)).Select(r => r.Id).Take(3).ToListAsync();

            Assert.Equal(
                condense($"SELECT TOP(@p) [r].[Id] FROM [Race] AS [r] ORDER BY {Combined} DESC"),
                condense(this.Db.Sql));

            Assert.Equal([12, 11, 10], ids);
        }

        [Fact]
        public async Task Combined_value_can_be_aggregated()
        {
            var latest = await this.Db.Race.MaxAsync(r => r.Date.At(r.ScheduledStartTime));

            Assert.Equal(
                condense($"SELECT MAX({Combined}) FROM [Race] AS [r]"),
                condense(this.Db.Sql));

            Assert.Equal(Iso(new LocalDateTime(2019, 12, 1, 19, 11, 11, 11)), Iso(latest));
        }

        /// <summary>The first and last representable date, with midnight and the last tick of the day.</summary>
        [Fact]
        public async Task Combined_value_is_exact_at_the_extremes()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime())
                .Options);
            using var tx = await db.Database.BeginTransactionAsync();

            var lastTick = LocalTime.FromHourMinuteSecondNanosecond(23, 59, 59, 999_999_900);
            var first = NewRace(new LocalDate(1, 1, 1), LocalTime.Midnight);
            var last = NewRace(new LocalDate(9999, 12, 31), lastTick);
            db.Race.AddRange(first, last);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var rows = await db.Race
                .Where(r => r.Id == first.Id || r.Id == last.Id)
                .OrderBy(r => r.Id)
                .Select(r => new { Combined = r.Date.At(r.ScheduledStartTime), Matches = r.Date + r.ScheduledStartTime == r.ScheduledStart })
                .ToListAsync();

            // EF Core could also compute this projection in .NET, so check that SQL Server did it.
            Assert.Contains($"SELECT {Combined} AS [Combined]", condense(db.Sql));

            Assert.Equal("0001-01-01T00:00:00", Iso(rows[0].Combined));
            Assert.Equal("9999-12-31T23:59:59.9999999", Iso(rows[1].Combined));
            Assert.All(rows, row => Assert.True(row.Matches));

            static Race NewRace(LocalDate date, LocalTime time) => new()
            {
                Date = date,
                ScheduledStartTime = time,
                ScheduledStart = date.At(time),
                DateTimeDate = new DateTime(2019, 1, 1),
                ScheduledDuration = Duration.Zero,
            };
        }

        // ---- Parameters on one side ----

        [Fact]
        public async Task LocalDate_parameter_At_LocalTime_column()
        {
            var date = new LocalDate(2019, 6, 1);
            var races = await this.Db.Race.Where(r => date.At(r.ScheduledStartTime) > new LocalDateTime(2019, 6, 1, 12, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE DATEADD(day, DATEDIFF(day, '19000101', @date), CAST([r].[ScheduledStartTime] AS datetime2)) > '2019-06-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            // Races 5-12 start after midday.
            Assert.Equal(8, races.Count);
        }

        [Fact]
        public async Task LocalDate_column_plus_LocalTime_parameter()
        {
            var time = new LocalTime(12, 0);
            var races = await this.Db.Race.Where(r => r.Date + time >= new LocalDateTime(2019, 6, 1, 12, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE DATEADD(day, DATEDIFF(day, '19000101', [r].[Date]), CAST(@time AS datetime2)) >= '2019-06-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(7, races.Count);
        }

        // ---- Nullable values ----

        /// <summary>TypeResults row 1 has LocalDateNull = 2000-02-29 and LocalTimeNull = 12:30; row 2 has both null.</summary>
        [Fact]
        public async Task Nullable_LocalDate_plus_nullable_LocalTime_is_null_when_either_is_null()
        {
            var values = await this.Db.TypeResults.OrderBy(t => t.Id).Select(t => t.LocalDateNull + t.LocalTimeNull).ToListAsync();

            Assert.Equal(
                condense("SELECT DATEADD(day, DATEDIFF(day, '19000101', [t].[LocalDateNull]), CAST([t].[LocalTimeNull] AS datetime2)) FROM [TypeResults] AS [t] ORDER BY [t].[Id]"),
                condense(this.Db.Sql));

            Assert.Equal(["2000-02-29T12:30:00", "null"], values.Select(v => Iso(v)));
        }

        [Fact]
        public async Task Nullable_LocalDate_plus_nullable_LocalTime_in_where()
        {
            var ids = await this.Db.TypeResults
                .Where(t => t.LocalDateNull + t.LocalTimeNull == new LocalDateTime(2000, 2, 29, 12, 30))
                .Select(t => t.Id)
                .ToListAsync();

            Assert.Equal(
                condense("SELECT [t].[Id] FROM [TypeResults] AS [t] WHERE DATEADD(day, DATEDIFF(day, '19000101', [t].[LocalDateNull]), CAST([t].[LocalTimeNull] AS datetime2)) = '2000-02-29T12:30:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal([1], ids);
        }

        [Fact]
        public async Task Nullable_LocalDate_At_nullable_LocalTime_Value()
        {
            var ids = await this.Db.TypeResults
                .Where(t => t.LocalDateNull.Value.At(t.LocalTimeNull.Value) == new LocalDateTime(2000, 2, 29, 12, 30))
                .Select(t => t.Id)
                .ToListAsync();

            Assert.Equal([1], ids);
        }

        // ---- AtMidnight and TimeOfDay ----

        [Fact]
        public async Task LocalDate_AtMidnight()
        {
            var races = await this.Db.Race.Where(r => r.Date.AtMidnight() > new LocalDateTime(2019, 6, 1, 0, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE CAST([r].[Date] AS datetime2) > '2019-06-01T00:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, races.Count);
        }

        [Fact]
        public async Task LocalDateTime_TimeOfDay()
        {
            var races = await this.Db.Race.Where(r => r.ScheduledStart.TimeOfDay > new LocalTime(12, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE CAST([r].[ScheduledStart] AS time) > '12:00:00'"),
                condense(this.Db.Sql));

            // Races 5-12 start after midday.
            Assert.Equal(8, races.Count);
        }

        [Fact]
        public async Task LocalDateTime_TimeOfDay_equals_the_stored_LocalTime()
        {
            var races = await this.Db.Race.Where(r => r.ScheduledStart.TimeOfDay == r.ScheduledStartTime).ToListAsync();

            Assert.Equal(
                condense($"{RaceSelectStatement} WHERE CAST([r].[ScheduledStart] AS time) = [r].[ScheduledStartTime]"),
                condense(this.Db.Sql));

            Assert.Equal(12, races.Count);
        }

        /// <summary>OffsetDateTime.TimeOfDay is the local time in its own offset, and so is CAST(datetimeoffset AS time).</summary>
        [Fact]
        public async Task OffsetDateTime_TimeOfDay()
        {
            // RaceResult n starts at (7 + n):00 +05:00.
            var results = await this.Db.RaceResult.Where(r => r.StartTimeOffset.TimeOfDay >= new LocalTime(12, 0)).ToListAsync();

            Assert.Equal(
                condense($"{RaceResultSelectStatement} WHERE CAST([r].[StartTimeOffset] AS time) >= '12:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(8, results.Count);
        }
    }
}
