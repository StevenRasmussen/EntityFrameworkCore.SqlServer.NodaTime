using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// NodaTime values and translated members/methods in the SELECT list. Computed values (e.g. DATEADD results)
    /// must carry the NodaTime type mapping so they are converted back when materialized.
    /// Seed data used: Race 7 and RaceResult 7 (see RacingContext).
    /// </summary>
    public class ProjectionQueryTests : QueryTestBase
    {
        public ProjectionQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture) { }

        [Fact]
        public async Task LocalDate_projection()
        {
            var result = await this.Db.Race.Where(r => r.Id == 7)
                .Select(r => new { r.Date, r.Date.Year, NextDay = r.Date.PlusDays(1) })
                .SingleAsync();

            Assert.Equal(
                condense("SELECT TOP(2) [r].[Date], DATEPART(year, [r].[Date]) AS [Year], DATEADD(day, CAST(1 AS int), [r].[Date]) AS [NextDay] FROM [Race] AS [r] WHERE [r].[Id] = 7"),
                condense(this.Db.Sql));

            Assert.Equal(Iso(new LocalDate(2019, 7, 1)), Iso(result.Date));
            Assert.Equal(2019, result.Year);
            Assert.Equal(Iso(new LocalDate(2019, 7, 2)), Iso(result.NextDay));
        }

        [Fact]
        public async Task LocalDateTime_projection()
        {
            var result = await this.Db.Race.Where(r => r.Id == 7)
                .Select(r => new { r.ScheduledStart, r.ScheduledStart.Hour, NextHour = r.ScheduledStart.PlusHours(1) })
                .SingleAsync();

            Assert.Equal(
                condense("SELECT TOP(2) [r].[ScheduledStart], DATEPART(hour, [r].[ScheduledStart]) AS [Hour], DATEADD(hour, CAST(CAST(1 AS bigint) AS int), [r].[ScheduledStart]) AS [NextHour] FROM [Race] AS [r] WHERE [r].[Id] = 7"),
                condense(this.Db.Sql));

            Assert.Equal(Iso(new LocalDateTime(2019, 7, 1, 14, 6, 6, 6)), Iso(result.ScheduledStart));
            Assert.Equal(14, result.Hour);
            Assert.Equal(Iso(new LocalDateTime(2019, 7, 1, 15, 6, 6, 6)), Iso(result.NextHour));
        }

        [Fact]
        public async Task LocalTime_projection()
        {
            var result = await this.Db.Race.Where(r => r.Id == 7)
                .Select(r => new { r.ScheduledStartTime, r.ScheduledStartTime.Minute, Later = r.ScheduledStartTime.PlusMinutes(10) })
                .SingleAsync();

            Assert.Equal(
                condense("SELECT TOP(2) [r].[ScheduledStartTime], DATEPART(minute, [r].[ScheduledStartTime]) AS [Minute], DATEADD(minute, CAST(CAST(10 AS bigint) AS int), [r].[ScheduledStartTime]) AS [Later] FROM [Race] AS [r] WHERE [r].[Id] = 7"),
                condense(this.Db.Sql));

            Assert.Equal(Iso(new LocalTime(14, 6, 6, 6)), Iso(result.ScheduledStartTime));
            Assert.Equal(6, result.Minute);
            Assert.Equal(Iso(new LocalTime(14, 16, 6, 6)), Iso(result.Later));
        }

        [Fact]
        public async Task Duration_projection()
        {
            var result = await this.Db.Race.Where(r => r.Id == 7)
                .Select(r => new { r.ScheduledDuration, r.ScheduledDuration.Hours, Longer = r.ScheduledDuration.PlusHours(1) })
                .SingleAsync();

            Assert.Equal(
                condense("SELECT TOP(2) [r].[ScheduledDuration], DATEPART(hour, [r].[ScheduledDuration]) AS [Hours], DATEADD(hour, CAST(1.0E0 AS int), [r].[ScheduledDuration]) AS [Longer] FROM [Race] AS [r] WHERE [r].[Id] = 7"),
                condense(this.Db.Sql));

            Assert.Equal(Iso(DurationExtensions.FromParts(7, 6, 6, 6)), Iso(result.ScheduledDuration));
            Assert.Equal(7, result.Hours);
            Assert.Equal(Iso(DurationExtensions.FromParts(8, 6, 6, 6)), Iso(result.Longer));
        }

        [Fact]
        public async Task Instant_projection()
        {
            var result = await this.Db.RaceResult.Where(r => r.Id == 7)
                .Select(r => new { r.StartTime, Year = r.StartTime.Year(), Later = r.StartTime.PlusHours(1) })
                .SingleAsync();

            Assert.Equal(
                condense("SELECT TOP(2) [r].[StartTime], DATEPART(year, [r].[StartTime]) AS [Year], DATEADD(hour, CAST(1.0E0 AS int), [r].[StartTime]) AS [Later] FROM [RaceResult] AS [r] WHERE [r].[Id] = 7"),
                condense(this.Db.Sql));

            Assert.Equal(Iso(Instant.FromUtc(2019, 7, 1, 14, 0)), Iso(result.StartTime));
            Assert.Equal(2019, result.Year);
            Assert.Equal(Iso(Instant.FromUtc(2019, 7, 1, 15, 0)), Iso(result.Later));
        }

        [Fact]
        public async Task OffsetDateTime_projection()
        {
            var result = await this.Db.RaceResult.Where(r => r.Id == 7)
                .Select(r => new { r.StartTimeOffset, r.StartTimeOffset.Day, Later = r.StartTimeOffset.PlusHours(1) })
                .SingleAsync();

            Assert.Equal(
                condense("SELECT TOP(2) [r].[StartTimeOffset], DATEPART(day, [r].[StartTimeOffset]) AS [Day], DATEADD(hour, CAST(1 AS int), [r].[StartTimeOffset]) AS [Later] FROM [RaceResult] AS [r] WHERE [r].[Id] = 7"),
                condense(this.Db.Sql));

            var offset = Offset.FromHours(5);
            Assert.Equal(Iso(new OffsetDateTime(new LocalDateTime(2019, 7, 1, 14, 0), offset)), Iso(result.StartTimeOffset));
            Assert.Equal(1, result.Day);
            Assert.Equal(Iso(new OffsetDateTime(new LocalDateTime(2019, 7, 1, 15, 0), offset)), Iso(result.Later));
        }

        [Fact]
        public async Task Nullable_projection()
        {
            var results = await this.Db.TypeResults.Where(t => t.Id <= 2).OrderBy(t => t.Id)
                .Select(t => new { t.DurationNull, t.InstantNull, t.LocalDateNull, t.LocalDateTimeNull, t.LocalTimeNull, t.OffsetDateTimeNull })
                .ToListAsync();

            Assert.Equal(
                condense("SELECT [t].[DurationNull], [t].[InstantNull], [t].[LocalDateNull], [t].[LocalDateTimeNull], [t].[LocalTimeNull], [t].[OffsetDateTimeNull] FROM [TypeResults] AS [t] WHERE [t].[Id] <= 2 ORDER BY [t].[Id]"),
                condense(this.Db.Sql));

            Assert.Equal(2, results.Count);

            var populated = results[0];
            Assert.Equal(Iso(Duration.FromMinutes(90)), Iso(populated.DurationNull));
            Assert.Equal(Iso(Instant.FromUtc(2020, 2, 29, 12, 30)), Iso(populated.InstantNull));
            Assert.Equal(Iso(new LocalDate(2000, 2, 29)), Iso(populated.LocalDateNull));
            Assert.Equal(Iso(new LocalDateTime(2020, 2, 29, 12, 30)), Iso(populated.LocalDateTimeNull));
            Assert.Equal(Iso(new LocalTime(12, 30)), Iso(populated.LocalTimeNull));
            Assert.Equal(Iso(new OffsetDateTime(new LocalDateTime(2020, 2, 29, 12, 30), Offset.FromHoursAndMinutes(5, 30))), Iso(populated.OffsetDateTimeNull));

            var nulls = results[1];
            Assert.Null(nulls.DurationNull);
            Assert.Null(nulls.InstantNull);
            Assert.Null(nulls.LocalDateNull);
            Assert.Null(nulls.LocalDateTimeNull);
            Assert.Null(nulls.LocalTimeNull);
            Assert.Null(nulls.OffsetDateTimeNull);
        }
    }
}
