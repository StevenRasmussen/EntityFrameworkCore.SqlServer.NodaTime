using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    public class LocalDateTimeQueryTests : QueryTestBase
    {
        public LocalDateTimeQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture) { }

        [Fact]
        public async Task LocalDateTime_can_be_used_in_query()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart >= new LocalDateTime(2019, 7, 1, 0, 0)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE [r].[ScheduledStart] >= '2019-07-01T00:00:00.0000000'"),
               condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_can_be_used_in_query_with_parameter()
        {
            var scheduledStart = new LocalDateTime(2019, 7, 1, 0, 0);
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart >= scheduledStart).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE [r].[ScheduledStart] >= @scheduledStart"),
               condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusYears()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusYears(1) >= new LocalDateTime(2019, 7, 1, 0, 0)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEADD(year, CAST(1 AS int), [r].[ScheduledStart]) >= '2019-07-01T00:00:00.0000000'"),
               condense(this.Db.Sql));

            Assert.Equal(12, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusMonths()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusMonths(1) >= new LocalDateTime(2019, 7, 1, 0, 0)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEADD(month, CAST(1 AS int), [r].[ScheduledStart]) >= '2019-07-01T00:00:00.0000000'"),
               condense(this.Db.Sql));

            Assert.Equal(7, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusDays()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusDays(45) >= new LocalDateTime(2019, 7, 1, 0, 0)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEADD(day, CAST(45 AS int), [r].[ScheduledStart]) >= '2019-07-01T00:00:00.0000000'"),
               condense(this.Db.Sql));

            Assert.Equal(7, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusWeeks()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusWeeks(5) >= new LocalDateTime(2019, 7, 1, 0, 0)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEADD(week, CAST(5 AS int), [r].[ScheduledStart]) >= '2019-07-01T00:00:00.0000000'"),
               condense(this.Db.Sql));

            Assert.Equal(7, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusQuarters()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusQuarters(1) >= new LocalDateTime(2019, 7, 1, 0, 0)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEADD(quarter, CAST(1 AS int), [r].[ScheduledStart]) >= '2019-07-01T00:00:00.0000000'"),
               condense(this.Db.Sql));

            Assert.Equal(9, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusHours()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusHours(2) >= new LocalDateTime(2019, 7, 1, 12, 0, 0)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(hour, CAST(CAST(2 AS bigint) AS int), [r].[ScheduledStart]) >= '2019-07-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusMinutes()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusMinutes(120) >= new LocalDateTime(2019, 7, 1, 12, 0, 0)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(minute, CAST(CAST(120 AS bigint) AS int), [r].[ScheduledStart]) >= '2019-07-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusSeconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusSeconds(7200) >= new LocalDateTime(2019, 7, 1, 12, 0, 0)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(second, CAST(CAST(7200 AS bigint) AS int), [r].[ScheduledStart]) >= '2019-07-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusMilliseconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusMilliseconds(7200000) >= new LocalDateTime(2019, 7, 1, 12, 0, 0)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(millisecond, CAST(CAST(7200000 AS bigint) AS int), [r].[ScheduledStart]) >= '2019-07-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusMicroseconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusMicroseconds(7200000) >= new LocalDateTime(2019, 7, 1, 12, 0, 0)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(microsecond, CAST(CAST(7200000 AS bigint) AS int), [r].[ScheduledStart]) >= '2019-07-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_PlusNanoseconds()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.PlusNanoseconds(7200000) >= new LocalDateTime(2019, 7, 1, 12, 0, 0)).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEADD(nanosecond, CAST(CAST(7200000 AS bigint) AS int), [r].[ScheduledStart]) >= '2019-07-01T12:00:00.0000000'"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_Date()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Date >= new LocalDate(2019, 7, 1)).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE CAST([r].[ScheduledStart] AS date) >= '2019-07-01'"),
               condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Year()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Year == 2019).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEPART(year, [r].[ScheduledStart]) = 2019"),
               condense(this.Db.Sql));

            Assert.Equal(12, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Quarter()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Quarter() == 4).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEPART(quarter, [r].[ScheduledStart]) = 4"),
               condense(this.Db.Sql));

            Assert.Equal(3, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Month()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Month == 12).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEPART(month, [r].[ScheduledStart]) = 12"),
               condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_DayOfYear()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.DayOfYear == 1).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEPART(dayofyear, [r].[ScheduledStart]) = 1"),
               condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Day()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Day == 1).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEPART(day, [r].[ScheduledStart]) = 1"),
               condense(this.Db.Sql));

            Assert.Equal(12, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Week()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Week() == 1).ToListAsync();

            Assert.Equal(
               condense(@$"{RaceSelectStatement} WHERE DATEPART(week, [r].[ScheduledStart]) = 1"),
               condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Hour()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Hour == 12).ToListAsync();

            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(hour, [r].[ScheduledStart]) = 12"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Minute()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Minute == 6).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(minute, [r].[ScheduledStart]) = 6"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Second()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Second == 6).ToListAsync();
            Assert.Equal(
                 condense(@$"{RaceSelectStatement} WHERE DATEPART(second, [r].[ScheduledStart]) = 6"),
                 condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Millisecond()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Millisecond == 6).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(millisecond, [r].[ScheduledStart]) = 6"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Microsecond()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.Microsecond() == 1000).ToListAsync();
            Assert.Equal(
                 condense(@$"{RaceSelectStatement} WHERE DATEPART(microsecond, [r].[ScheduledStart]) = 1000"),
                 condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DatePart_Nanosecond()
        {
            var raceResults = await this.Db.Race.Where(r => r.ScheduledStart.NanosecondOfSecond == 1000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEPART(nanosecond, [r].[ScheduledStart]) = 1000000"),
                condense(this.Db.Sql));

            Assert.Single(raceResults);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Year()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffYear(r.ScheduledStart, LocalDateTimeExtensions.FromParts(2020, 6, 1, 0, 0, 0, 0, 0, 0)) == 1).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF(YEAR, [r].[ScheduledStart], '2020-06-01T00:00:00.0000000') = 1"),
                condense(this.Db.Sql));

            Assert.Equal(12, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Month()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffMonth(r.ScheduledStart, LocalDateTimeExtensions.FromParts(2020, 1, 1, 0, 0, 0, 0, 0, 0)) >= 6).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF(MONTH, [r].[ScheduledStart], '2020-01-01T00:00:00.0000000') >= 6"),
                condense(this.Db.Sql));

            Assert.Equal(7, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Week()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffWeek(r.ScheduledStart, LocalDateTimeExtensions.FromParts(2020, 1, 1, 0, 0, 0, 0, 0, 0)) >= 30).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF(WEEK, [r].[ScheduledStart], '2020-01-01T00:00:00.0000000') >= 30"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Day()
        {
            var raceResults = await this.Db.Race.Where(r => this.Functions.DateDiffDay(r.ScheduledStart, LocalDateTimeExtensions.FromParts(2020, 1, 1, 0, 0, 0, 0, 0, 0)) >= 200).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSelectStatement} WHERE DATEDIFF(DAY, [r].[ScheduledStart], '2020-01-01T00:00:00.0000000') >= 200"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Hour()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffHour(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) >= 4).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF(HOUR, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) >= 4"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Minute()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffMinute(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) >= 244).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF(MINUTE, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) >= 244"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Second()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffSecond(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) >= 14500).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF(SECOND, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) >= 14500"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiff_Millisecond()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffMillisecond(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) <= 15000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF(MILLISECOND, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) <= 15000000"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiffBig_Second()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffBigSecond(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) >= 10000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF_BIG(SECOND, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) >= CAST(10000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(6, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiffBig_Millisecond()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffBigMillisecond(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) <= 15000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF_BIG(MILLISECOND, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) <= CAST(15000000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiffBig_Microsecond()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffBigMicrosecond(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) <= 15000000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF_BIG(MICROSECOND, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) <= CAST(15000000000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }

        [Fact]
        public async Task LocalDateTime_DateDiffBig_Nanosecond()
        {
            var raceResults = await this.Db.RaceSplit.Where(r => this.Functions.DateDiffBigNanosecond(LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), r.TimeStampLocalDateTime) <= 15000000000000).ToListAsync();
            Assert.Equal(
                condense(@$"{RaceSplitSelectStatement} WHERE DATEDIFF_BIG(NANOSECOND, '2020-01-01T01:01:00.0010010', [r].[TimeStampLocalDateTime]) <= CAST(15000000000000 AS bigint)"),
                condense(this.Db.Sql));

            Assert.Equal(5, raceResults.Count);
        }
    }
}
