using Microsoft.EntityFrameworkCore;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Queries and round-trips against columns with non-default store types or precision (see <see cref="PrecisionNodaTypes"/>).
    /// Seed data: row 1 = 2019-01-01 08:00, row 2 = 2019-07-01 14:30.
    /// </summary>
    public class PrecisionQueryTests : QueryTestBase
    {
        private const string CountStatement = "SELECT COUNT(*) FROM [PrecisionTypes] AS [p]";

        public PrecisionQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture) { }

        [Fact]
        public async Task Instant_datetime_column_can_be_used_in_query()
        {
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.InstantAsDateTime >= Instant.FromUtc(2019, 6, 1, 0, 0));

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[InstantAsDateTime] >= '2019-06-01T00:00:00.000'"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Instant_datetime_column_can_be_used_in_query_with_parameter()
        {
            var instant = Instant.FromUtc(2019, 6, 1, 0, 0);
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.InstantAsDateTime >= instant);

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[InstantAsDateTime] >= @instant"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Instant_smalldatetime_column_can_be_used_in_query()
        {
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.InstantAsSmallDateTime >= Instant.FromUtc(2019, 6, 1, 0, 0));

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[InstantAsSmallDateTime] >= '2019-06-01T00:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Instant_smalldatetime_column_can_be_used_in_query_with_parameter()
        {
            var instant = Instant.FromUtc(2019, 6, 1, 0, 0);
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.InstantAsSmallDateTime >= instant);

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[InstantAsSmallDateTime] >= @instant"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task LocalDateTime_datetime_column_can_be_used_in_query()
        {
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.LocalDateTimeAsDateTime >= new LocalDateTime(2019, 6, 1, 0, 0));

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[LocalDateTimeAsDateTime] >= '2019-06-01T00:00:00.000'"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task LocalDateTime_datetime_column_can_be_used_in_query_with_parameter()
        {
            var localDateTime = new LocalDateTime(2019, 6, 1, 0, 0);
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.LocalDateTimeAsDateTime >= localDateTime);

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[LocalDateTimeAsDateTime] >= @localDateTime"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task LocalDateTime_smalldatetime_column_can_be_used_in_query()
        {
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.LocalDateTimeAsSmallDateTime >= new LocalDateTime(2019, 6, 1, 0, 0));

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[LocalDateTimeAsSmallDateTime] >= '2019-06-01T00:00:00'"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task LocalDateTime_smalldatetime_column_can_be_used_in_query_with_parameter()
        {
            var localDateTime = new LocalDateTime(2019, 6, 1, 0, 0);
            var count = await this.Db.PrecisionTypes.CountAsync(p => p.LocalDateTimeAsSmallDateTime >= localDateTime);

            Assert.Equal(
                condense($"{CountStatement} WHERE [p].[LocalDateTimeAsSmallDateTime] >= @localDateTime"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Values_are_rounded_to_the_column_precision_when_saved()
        {
            await using var transaction = await this.Db.Database.BeginTransactionAsync();

            // Inputs are chosen so that rounding and truncation give the same result:
            //   .5004s     -> datetime (1/300s) = .500, smalldatetime (1 minute) = 03:04:00
            //   .1234567s  -> precision 3 = .123, precision 2 = .12, precision 0 = 03:04:05
            var second = new LocalDateTime(2020, 1, 2, 3, 4, 5);
            var coarse = second.PlusTicks(5_004_000);
            var fine = second.PlusTicks(1_234_567);
            var offset = Offset.FromHoursAndMinutes(5, 30);

            var entity = new PrecisionNodaTypes
            {
                InstantAsDateTime = coarse.InUtc().ToInstant(),
                InstantAsSmallDateTime = coarse.InUtc().ToInstant(),
                InstantAsDateTime2Of0 = fine.InUtc().ToInstant(),
                InstantWithPrecision3 = fine.InUtc().ToInstant(),
                LocalDateTimeAsDateTime = coarse,
                LocalDateTimeAsSmallDateTime = coarse,
                LocalDateTimeAsDateTime2Of0 = fine,
                LocalDateTimeWithPrecision3 = fine,
                OffsetDateTimeAsDateTimeOffsetOf3 = new OffsetDateTime(fine, offset),
                OffsetDateTimeWithPrecision2 = new OffsetDateTime(fine, offset),
                LocalTimeAsTimeOf3 = fine.TimeOfDay,
                LocalTimeWithPrecision2 = fine.TimeOfDay,
                DurationAsTimeOf3 = Duration.FromTicks(fine.TimeOfDay.TickOfDay),
                DurationWithPrecision2 = Duration.FromTicks(fine.TimeOfDay.TickOfDay),
            };

            this.Db.Add(entity);
            await this.Db.SaveChangesAsync();
            this.Db.Entry(entity).State = EntityState.Detached;

            var fromDb = await this.Db.PrecisionTypes.AsNoTracking().SingleAsync(p => p.Id == entity.Id);

            var minute = new LocalDateTime(2020, 1, 2, 3, 4);
            Assert.Equal(Iso(second.PlusMilliseconds(500).InUtc().ToInstant()), Iso(fromDb.InstantAsDateTime));
            Assert.Equal(Iso(minute.InUtc().ToInstant()), Iso(fromDb.InstantAsSmallDateTime));
            Assert.Equal(Iso(second.InUtc().ToInstant()), Iso(fromDb.InstantAsDateTime2Of0));
            Assert.Equal(Iso(second.PlusMilliseconds(123).InUtc().ToInstant()), Iso(fromDb.InstantWithPrecision3));

            Assert.Equal(Iso(second.PlusMilliseconds(500)), Iso(fromDb.LocalDateTimeAsDateTime));
            Assert.Equal(Iso(minute), Iso(fromDb.LocalDateTimeAsSmallDateTime));
            Assert.Equal(Iso(second), Iso(fromDb.LocalDateTimeAsDateTime2Of0));
            Assert.Equal(Iso(second.PlusMilliseconds(123)), Iso(fromDb.LocalDateTimeWithPrecision3));

            Assert.Equal(Iso(new OffsetDateTime(second.PlusMilliseconds(123), offset)), Iso(fromDb.OffsetDateTimeAsDateTimeOffsetOf3));
            Assert.Equal(Iso(new OffsetDateTime(second.PlusMilliseconds(120), offset)), Iso(fromDb.OffsetDateTimeWithPrecision2));

            Assert.Equal(Iso(new LocalTime(3, 4, 5, 123)), Iso(fromDb.LocalTimeAsTimeOf3));
            Assert.Equal(Iso(new LocalTime(3, 4, 5, 120)), Iso(fromDb.LocalTimeWithPrecision2));
            Assert.Equal(Iso(Duration.FromTicks(new LocalTime(3, 4, 5, 123).TickOfDay)), Iso(fromDb.DurationAsTimeOf3));
            Assert.Equal(Iso(Duration.FromTicks(new LocalTime(3, 4, 5, 120).TickOfDay)), Iso(fromDb.DurationWithPrecision2));
        }
    }
}
