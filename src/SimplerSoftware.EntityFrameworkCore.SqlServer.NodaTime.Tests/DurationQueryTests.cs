using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    public class DurationQueryTests : QueryTestBase
    {
        public DurationQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture) { }

        [Fact]
        public async Task Duration_can_be_used_in_query()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration >= Duration.FromHours(7)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE [r].[ScheduledDuration] >= '07:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task Duration_can_be_used_in_query_with_parameter()
        {
            var duration = Duration.FromHours(7);
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration >= duration).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE [r].[ScheduledDuration] >= @duration"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task Duration_PlusHours()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.PlusHours(2) >= Duration.FromHours(6)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(hour, CAST(2.0E0 AS int), [r].[ScheduledDuration]) >= '06:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(9, raceResults.Count);
        }

        [Fact]
        public async Task Duration_PlusMinutes()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.PlusMinutes(120) >= Duration.FromHours(6)).ToListAsync();

            Assert.Equal(
                 condense(@$"{RaceSelectStatement} WHERE DATEADD(minute, CAST(120.0E0 AS int), [r].[ScheduledDuration]) >= '06:00:00'"),
                 condense(this.Db.Sql));

            Assert.Equal(9, raceResults.Count);
        }

        [Fact]
        public async Task Duration_PlusSeconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.PlusSeconds(7200) >= Duration.FromHours(6)).ToListAsync();

            Assert.Equal(
                 condense(@$"{RaceSelectStatement} WHERE DATEADD(second, CAST(7200.0E0 AS int), [r].[ScheduledDuration]) >= '06:00:00'"),
                 condense(this.Db.Sql));

            Assert.Equal(9, raceResults.Count);
        }

        [Fact]
        public async Task Duration_PlusMilliseconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.PlusMilliseconds(7200000) >= Duration.FromHours(6)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(millisecond, CAST(7200000.0E0 AS int), [r].[ScheduledDuration]) >= '06:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(9, raceResults.Count);
        }

        [Fact]
        public async Task Duration_PlusMicroseconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.PlusMicroseconds(7200000) >= Duration.FromHours(6)).ToListAsync();

            Assert.Equal(
                 condense(@$"{RaceSelectStatement} WHERE DATEADD(microsecond, CAST(7200000.0E0 AS int), [r].[ScheduledDuration]) >= '06:00:00'"),
                 condense(this.Db.Sql));

            Assert.Equal(7, raceResults.Count);
        }

        [Fact]
        public async Task Duration_PlusNanoseconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.PlusNanoseconds(7200000) >= Duration.FromHours(6)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(nanosecond, CAST(7200000.0E0 AS int), [r].[ScheduledDuration]) >= '06:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(7, raceResults.Count);
        }

        /// <summary>DATEPART(day, ...) is invalid for a time column, which is what Days used to translate to.</summary>
        [Fact]
        public async Task Duration_Days()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.Days == 0).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF(day, '19000101', [r].[ScheduledDuration]) = 0"),
                condense(this.Db.Sql));

            // A time column holds less than 24 hours, so every duration has 0 whole days.
            Assert.Equal(12, raceResults.Count);
        }

        /// <summary>TypeResults row 1 has DurationNull = 90 minutes; row 2 has NULL, which must not match.</summary>
        [Fact]
        public async Task Duration_Days_of_nullable_duration()
        {
            var ids = await this.Db.TypeResults.Where(t => t.DurationNull.Value.Days == 0).Select(t => t.Id).ToListAsync();

            Assert.Equal(
                condense("SELECT [t].[Id] FROM [TypeResults] AS [t] WHERE DATEDIFF(day, '19000101', [t].[DurationNull]) = 0"),
                condense(this.Db.Sql));

            Assert.Equal([1], ids);
        }

        [Fact]
        public async Task Duration_DatePart_Hour()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.Hours == 12).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(hour, [r].[ScheduledDuration]) = 12"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task Duration_DatePart_Minute()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.Minutes == 6).ToListAsync();
            Assert.Equal(
                 condense(@$"{RaceSelectStatement} WHERE DATEPART(minute, [r].[ScheduledDuration]) = 6"),
                 condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task Duration_DatePart_Second()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.Seconds == 6).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(second, [r].[ScheduledDuration]) = 6"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task Duration_DatePart_Millisecond()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.Milliseconds == 6).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(millisecond, [r].[ScheduledDuration]) = 6"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task Duration_DatePart_Microsecond()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.Microseconds() == 1000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(microsecond, [r].[ScheduledDuration]) = 1000"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task Duration_DatePart_Nanosecond()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledDuration.SubsecondNanoseconds == 1000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(nanosecond, [r].[ScheduledDuration]) = 1000000"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task Duration_DateDiff_Second()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffSecond(DurationExtensions.FromParts(0, 0, 0, 1, 0, 0), r.ScheduledDuration) >= 15000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF(SECOND, '00:00:00.0010000', [r].[ScheduledDuration]) >= 15000"),
                condense(this.Db.Sql));

            Assert.Equal(8, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiff_Millisecond()
        {
            var raceResults = await this.Db.RaceResult.Where(r => this.Functions.DateDiffMillisecond(DurationExtensions.FromParts(0, 0, 0, 1, 0, 0), r.OffsetFromWinner) >= 6).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceResultSelectStatement} WHERE DATEDIFF(MILLISECOND, '00:00:00.0010000', [r].[OffsetFromWinner]) >= 6"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiff_Microsecond()
        {
            var raceResults = await this.Db.RaceResult.Where(r => this.Functions.DateDiffMicrosecond(DurationExtensions.FromParts(0, 0, 0, 1, 0, 0), r.OffsetFromWinner) >= 5000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceResultSelectStatement} WHERE DATEDIFF(MICROSECOND, '00:00:00.0010000', [r].[OffsetFromWinner]) >= 5000"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiff_Nanosecond()
        {
            var raceResults = await this.Db.RaceResult.Where(r => this.Functions.DateDiffNanosecond(DurationExtensions.FromParts(0, 0, 0, 1, 0, 0), r.OffsetFromWinner) <= 150000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceResultSelectStatement} WHERE DATEDIFF(NANOSECOND, '00:00:00.0010000', [r].[OffsetFromWinner]) <= 150000"),
                condense(this.Db.Sql));

            Assert.Equal(2, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiffBig_Second()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffBigSecond(DurationExtensions.FromParts(0, 1, 1, 1, 1, 1), r.ScheduledDuration) >= 10000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF_BIG(SECOND, '00:01:01.0010010', [r].[ScheduledDuration]) >= CAST(10000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(10, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiffBig_Millisecond()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffBigMillisecond(DurationExtensions.FromParts(1, 0, 0, 0, 0, 0), r.ScheduledDuration) <= 15000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF_BIG(MILLISECOND, '01:00:00', [r].[ScheduledDuration]) <= CAST(15000000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiffBig_Microsecond()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffBigMicrosecond(DurationExtensions.FromParts(1, 0, 0, 0, 0, 0), r.ScheduledDuration) <= 15000000000).ToListAsync();
            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEDIFF_BIG(MICROSECOND, '01:00:00', [r].[ScheduledDuration]) <= CAST(15000000000 AS bigint)"),
               condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task Duration_DateDiffBig_Nanosecond()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffBigNanosecond(DurationExtensions.FromParts(1, 0, 0, 0, 0, 0), r.ScheduledDuration) <= 15000000000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF_BIG(NANOSECOND, '01:00:00', [r].[ScheduledDuration]) <= CAST(15000000000000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }
    }
}
