using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using NodaTime;

using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;

using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Writes values through the value converters and parameter configuration, then reads them back
    /// (AsNoTracking, so values are materialized from the database rather than the change tracker).
    /// </summary>
    public class SupportedTypesTests : QueryTestBase
    {
        public SupportedTypesTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        [Fact]
        public async Task AllSupported_NodatimeTypes_RoundTrip_DefaultValues()
        {
            await using var transaction = await this.Db.Database.BeginTransactionAsync();

            // Defaults are edge values: 0001-01-01 dates, the Unix epoch, midnight, Duration.Zero, and nulls.
            var saved = await this.SaveAndDetachAsync(new SupportedNodaTypes());

            AssertAllPropertiesEqual(saved, await this.Db.TypeResults.AsNoTracking().SingleAsync(x => x.Id == saved.Id));
        }

        [Fact]
        public async Task AllSupported_NodatimeTypes_RoundTrip_FullPrecisionValues()
        {
            await using var transaction = await this.Db.Database.BeginTransactionAsync();

            var saved = await this.SaveAndDetachAsync(CreateFullPrecisionValues());

            AssertAllPropertiesEqual(saved, await this.Db.TypeResults.AsNoTracking().SingleAsync(x => x.Id == saved.Id));
        }

        [Fact]
        public async Task AllSupported_NodatimeTypes_CanBeFound_ByFullPrecisionParameter()
        {
            await using var transaction = await this.Db.Database.BeginTransactionAsync();

            var saved = await this.SaveAndDetachAsync(CreateFullPrecisionValues());

            var duration = saved.DurationNonNull;
            var instant = saved.InstantNonNull;
            var localDate = saved.LocalDateNonNull;
            var localDateTime = saved.LocalDateTimeNonNull;
            var localTime = saved.LocalTimeNonNull;
            var offsetDateTime = saved.OffsetDateTimeNonNull;
            var nullableDuration = saved.DurationNull;
            var nullableInstant = saved.InstantNull;
            var nullableLocalDate = saved.LocalDateNull;
            var nullableLocalDateTime = saved.LocalDateTimeNull;
            var nullableLocalTime = saved.LocalTimeNull;
            var nullableOffsetDateTime = saved.OffsetDateTimeNull;

            await this.AssertFoundAsync(saved.Id, nameof(Duration), x => x.DurationNonNull == duration);
            await this.AssertFoundAsync(saved.Id, nameof(Instant), x => x.InstantNonNull == instant);
            await this.AssertFoundAsync(saved.Id, nameof(LocalDate), x => x.LocalDateNonNull == localDate);
            await this.AssertFoundAsync(saved.Id, nameof(LocalDateTime), x => x.LocalDateTimeNonNull == localDateTime);
            await this.AssertFoundAsync(saved.Id, nameof(LocalTime), x => x.LocalTimeNonNull == localTime);
            await this.AssertFoundAsync(saved.Id, nameof(OffsetDateTime), x => x.OffsetDateTimeNonNull == offsetDateTime);
            await this.AssertFoundAsync(saved.Id, "Duration?", x => x.DurationNull == nullableDuration);
            await this.AssertFoundAsync(saved.Id, "Instant?", x => x.InstantNull == nullableInstant);
            await this.AssertFoundAsync(saved.Id, "LocalDate?", x => x.LocalDateNull == nullableLocalDate);
            await this.AssertFoundAsync(saved.Id, "LocalDateTime?", x => x.LocalDateTimeNull == nullableLocalDateTime);
            await this.AssertFoundAsync(saved.Id, "LocalTime?", x => x.LocalTimeNull == nullableLocalTime);
            await this.AssertFoundAsync(saved.Id, "OffsetDateTime?", x => x.OffsetDateTimeNull == nullableOffsetDateTime);
        }

        /// <summary>100ns (tick) precision, maximum values, a leap day and non-whole-hour offsets.</summary>
        private static SupportedNodaTypes CreateFullPrecisionValues() => new SupportedNodaTypes
        {
            DurationNonNull = Duration.FromHours(23) + Duration.FromTicks(1_234_567),
            DurationNull = Duration.FromTicks(1),
            InstantNonNull = Instant.FromUtc(2021, 3, 4, 5, 6, 7).PlusTicks(1_234_567),
            InstantNull = Instant.FromUtc(9999, 12, 31, 23, 59, 59).PlusTicks(9_999_999),
            LocalDateNonNull = new LocalDate(2000, 2, 29),
            LocalDateNull = new LocalDate(9999, 12, 31),
            LocalDateTimeNonNull = new LocalDateTime(2021, 3, 4, 5, 6, 7).PlusTicks(1_234_567),
            LocalDateTimeNull = new LocalDateTime(9999, 12, 31, 23, 59, 59).PlusTicks(9_999_999),
            LocalTimeNonNull = new LocalTime(23, 59, 59).PlusTicks(9_999_999),
            LocalTimeNull = new LocalTime(0, 0).PlusTicks(1),
            OffsetDateTimeNonNull = new OffsetDateTime(new LocalDateTime(2021, 3, 4, 5, 6, 7).PlusTicks(1_234_567), Offset.FromHoursAndMinutes(5, 30)),
            OffsetDateTimeNull = new OffsetDateTime(new LocalDateTime(2021, 3, 4, 5, 6, 7), Offset.FromHoursAndMinutes(-9, -30)),
        };

        private async Task<SupportedNodaTypes> SaveAndDetachAsync(SupportedNodaTypes entity)
        {
            this.Db.Add(entity);
            await this.Db.SaveChangesAsync();
            this.Db.Entry(entity).State = EntityState.Detached;
            return entity;
        }

        private async Task AssertFoundAsync(int id, string description, Expression<Func<SupportedNodaTypes, bool>> predicate)
        {
            var count = await this.Db.TypeResults.Where(x => x.Id == id).Where(predicate).CountAsync();
            Assert.True(count == 1, $"{description} parameter did not match the saved value. SQL: {this.Db.Sql}");
        }

        private static void AssertAllPropertiesEqual(SupportedNodaTypes expected, SupportedNodaTypes actual)
        {
            Assert.Equal(Iso(expected.DurationNonNull), Iso(actual.DurationNonNull));
            Assert.Equal(Iso(expected.DurationNull), Iso(actual.DurationNull));
            Assert.Equal(Iso(expected.InstantNonNull), Iso(actual.InstantNonNull));
            Assert.Equal(Iso(expected.InstantNull), Iso(actual.InstantNull));
            Assert.Equal(Iso(expected.LocalDateNonNull), Iso(actual.LocalDateNonNull));
            Assert.Equal(Iso(expected.LocalDateNull), Iso(actual.LocalDateNull));
            Assert.Equal(Iso(expected.LocalDateTimeNonNull), Iso(actual.LocalDateTimeNonNull));
            Assert.Equal(Iso(expected.LocalDateTimeNull), Iso(actual.LocalDateTimeNull));
            Assert.Equal(Iso(expected.LocalTimeNonNull), Iso(actual.LocalTimeNonNull));
            Assert.Equal(Iso(expected.LocalTimeNull), Iso(actual.LocalTimeNull));
            Assert.Equal(Iso(expected.OffsetDateTimeNonNull), Iso(actual.OffsetDateTimeNonNull));
            Assert.Equal(Iso(expected.OffsetDateTimeNull), Iso(actual.OffsetDateTimeNull));
        }
    }
}
