using System.Linq;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Tests that never open a database connection, so they can run anywhere (e.g. Linux CI without LocalDB).
    /// They exercise the pieces most likely to break on an EF Core major version bump: the plugin
    /// registration (which touches EF internal APIs), model/type-mapping resolution and LINQ -> SQL translation.
    /// Run just these with: dotnet test --filter "Category=Offline"
    /// </summary>
    [Trait("Category", "Offline")]
    public class OfflineCompatibilityTests
    {
        private static RacingContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer("Server=offline;Database=Offline;Trusted_Connection=True", x => x.UseNodaTime())
                .Options;
            return new RacingContext(options);
        }

        [Fact]
        public void Model_builds_and_maps_every_supported_type()
        {
            using var db = CreateContext();
            var entity = db.Model.FindEntityType(typeof(SupportedNodaTypes))!;

            var expected = new (string Property, string StoreType)[]
            {
                (nameof(SupportedNodaTypes.DurationNonNull), "time"),
                (nameof(SupportedNodaTypes.DurationNull), "time"),
                (nameof(SupportedNodaTypes.InstantNonNull), "datetime2"),
                (nameof(SupportedNodaTypes.InstantNull), "datetime2"),
                (nameof(SupportedNodaTypes.LocalDateNonNull), "date"),
                (nameof(SupportedNodaTypes.LocalDateNull), "date"),
                (nameof(SupportedNodaTypes.LocalDateTimeNonNull), "datetime2"),
                (nameof(SupportedNodaTypes.LocalDateTimeNull), "datetime2"),
                (nameof(SupportedNodaTypes.LocalTimeNonNull), "time"),
                (nameof(SupportedNodaTypes.LocalTimeNull), "time"),
                (nameof(SupportedNodaTypes.OffsetDateTimeNonNull), "datetimeoffset"),
                (nameof(SupportedNodaTypes.OffsetDateTimeNull), "datetimeoffset"),
            };

            foreach (var (property, storeType) in expected)
            {
                var mapping = entity.FindProperty(property)!.GetRelationalTypeMapping();
                Assert.Equal(storeType, mapping.StoreType);
            }
        }

        [Fact]
        public void Create_script_can_be_generated_without_a_database()
        {
            using var db = CreateContext();
            var script = db.Database.GenerateCreateScript();

            Assert.Contains("[DurationNonNull] time NOT NULL", script);
            Assert.Contains("[InstantNonNull] datetime2 NOT NULL", script);
            Assert.Contains("[LocalDateNonNull] date NOT NULL", script);
            Assert.Contains("[LocalDateTimeNull] datetime2 NULL", script);
            Assert.Contains("[OffsetDateTimeNonNull] datetimeoffset NOT NULL", script);
        }

        [Fact]
        public void LocalDate_comparison_translates()
        {
            using var db = CreateContext();
            var sql = db.Race.Where(r => r.Date >= new LocalDate(2019, 7, 1)).ToQueryString();

            Assert.Contains("[r].[Date] >= '2019-07-01'", sql);
        }

        [Fact]
        public void LocalDate_PlusMonths_translates_to_DATEADD()
        {
            using var db = CreateContext();
            var sql = db.Race.Where(r => r.Date.PlusMonths(1) >= new LocalDate(2019, 7, 1)).ToQueryString();

            Assert.Contains("DATEADD(month, CAST(1 AS int), [r].[Date])", sql);
        }

        [Fact]
        public void LocalDate_member_access_translates()
        {
            using var db = CreateContext();
            var sql = db.Race.Where(r => r.Date.Year == 2019).ToQueryString();

            Assert.Contains("DATEPART(year, [r].[Date])", sql);
        }

        [Fact]
        public void LocalDateTime_member_access_translates()
        {
            using var db = CreateContext();
            var sql = db.Race.Where(r => r.ScheduledStart.Hour == 8).ToQueryString();

            Assert.Contains("DATEPART(hour, [r].[ScheduledStart])", sql);
        }

        [Fact]
        public void LocalTime_comparison_translates()
        {
            using var db = CreateContext();
            var sql = db.Race.Where(r => r.ScheduledStartTime > new LocalTime(9, 0)).ToQueryString();

            Assert.Contains("[r].[ScheduledStartTime] >", sql);
        }

        [Fact]
        public void Instant_comparison_translates()
        {
            using var db = CreateContext();
            var sql = db.RaceResult.Where(r => r.StartTime >= Instant.FromUtc(2019, 7, 1, 0, 0)).ToQueryString();

            Assert.Contains("[r].[StartTime] >=", sql);
            Assert.Contains("2019-07-01", sql);
        }

        [Fact]
        public void OffsetDateTime_comparison_translates()
        {
            using var db = CreateContext();
            var sql = db.RaceResult.Where(r => r.StartTimeOffset.Year == 2019).ToQueryString();

            Assert.Contains("[r].[StartTimeOffset]", sql);
        }

        [Fact]
        public void Duration_comparison_translates()
        {
            using var db = CreateContext();
            var sql = db.Race.Where(r => r.ScheduledDuration > Duration.FromHours(2)).ToQueryString();

            Assert.Contains("[r].[ScheduledDuration] >", sql);
        }
    }
}
