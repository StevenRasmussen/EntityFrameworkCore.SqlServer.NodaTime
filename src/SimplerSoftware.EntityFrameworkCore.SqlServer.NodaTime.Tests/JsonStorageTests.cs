using Microsoft.EntityFrameworkCore;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// NodaTime values stored as JSON: list properties (JSON arrays) and a complex type mapped to a JSON column.
    /// Each test inserts rows A and B inside a transaction that is rolled back when the test ends.
    /// Row A holds the later values (2019+), row B the earlier ones (2017/2018).
    /// </summary>
    public class JsonStorageTests : QueryTestBase
    {
        public JsonStorageTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        private static readonly LocalTime AlmostMidnight = LocalTime.FromHourMinuteSecondNanosecond(23, 59, 59, 999_999_900);

        private static NodaTimeCollections RowA() => new()
        {
            Dates = [new LocalDate(2019, 2, 1), new LocalDate(2020, 12, 31)],
            Times = [new LocalTime(10, 1, 1, 1), AlmostMidnight],
            LocalDateTimes = [new LocalDateTime(2019, 2, 1, 10, 1, 1, 1), new LocalDate(2020, 12, 31).At(AlmostMidnight)],
            Instants = [Instant.FromUtc(2019, 2, 1, 10, 0), new LocalDate(2020, 12, 31).At(AlmostMidnight).InUtc().ToInstant()],
            OffsetDateTimes =
            [
                new OffsetDateTime(new LocalDateTime(2019, 2, 1, 10, 0), Offset.FromHoursAndMinutes(5, 30)),
                new OffsetDateTime(new LocalDate(2020, 12, 31).At(AlmostMidnight), Offset.FromHours(-8)),
            ],
            Durations = [Duration.FromHours(1) + Duration.FromMinutes(2) + Duration.FromSeconds(3) + Duration.FromMilliseconds(4), Duration.FromTicks(AlmostMidnight.TickOfDay)],
            Json = new NodaTimeJson
            {
                Date = new LocalDate(2019, 2, 1),
                NullableDate = null,
                Time = new LocalTime(10, 1, 1, 1),
                LocalDateTime = new LocalDateTime(2019, 2, 1, 10, 1, 1, 1),
                Instant = Instant.FromUtc(2019, 2, 1, 10, 0),
                OffsetDateTime = new OffsetDateTime(new LocalDateTime(2019, 2, 1, 10, 0), Offset.FromHoursAndMinutes(5, 30)),
                Duration = Duration.FromHours(1) + Duration.FromMinutes(2) + Duration.FromSeconds(3) + Duration.FromMilliseconds(4),
            },
        };

        private static NodaTimeCollections RowB() => new()
        {
            Dates = [new LocalDate(2017, 6, 15), new LocalDate(2018, 1, 1)],
            Times = [new LocalTime(8, 0), new LocalTime(9, 0)],
            LocalDateTimes = [new LocalDateTime(2017, 6, 15, 8, 0), new LocalDateTime(2018, 1, 1, 9, 0)],
            Instants = [Instant.FromUtc(2017, 6, 15, 8, 0), Instant.FromUtc(2018, 1, 1, 9, 0)],
            OffsetDateTimes =
            [
                new OffsetDateTime(new LocalDateTime(2017, 6, 15, 8, 0), Offset.FromHoursAndMinutes(5, 30)),
                new OffsetDateTime(new LocalDateTime(2018, 1, 1, 9, 0), Offset.FromHours(-8)),
            ],
            Durations = [Duration.FromMinutes(10), Duration.FromMinutes(30)],
            Json = new NodaTimeJson
            {
                Date = new LocalDate(2017, 6, 15),
                NullableDate = new LocalDate(2018, 1, 1),
                Time = new LocalTime(8, 0),
                LocalDateTime = new LocalDateTime(2017, 6, 15, 8, 0),
                Instant = Instant.FromUtc(2017, 6, 15, 8, 0),
                OffsetDateTime = new OffsetDateTime(new LocalDateTime(2017, 6, 15, 8, 0), Offset.FromHoursAndMinutes(5, 30)),
                Duration = Duration.FromMinutes(10),
            },
        };

        // ---- Round trips ----

        [Fact]
        public async Task Lists_round_trip()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, _) = await InsertRowsAsync(db);

            var expected = RowA();
            var actual = await db.NodaTimeCollections.SingleAsync(x => x.Id == a);

            AssertList(expected.Dates, actual.Dates);
            AssertList(expected.Times, actual.Times);
            AssertList(expected.LocalDateTimes, actual.LocalDateTimes);
            AssertList(expected.Instants, actual.Instants);
            AssertList(expected.OffsetDateTimes, actual.OffsetDateTimes);
            AssertList(expected.Durations, actual.Durations);
        }

        [Fact]
        public async Task Json_column_round_trips()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);

            var rows = await db.NodaTimeCollections.Where(x => x.Id == a || x.Id == b).OrderBy(x => x.Id).ToListAsync();

            foreach (var (expected, actual) in new[] { RowA().Json, RowB().Json }.Zip(rows.Select(r => r.Json)))
            {
                Assert.Equal(Iso(expected.Date), Iso(actual.Date));
                Assert.Equal(Iso(expected.NullableDate), Iso(actual.NullableDate));
                Assert.Equal(Iso(expected.Time), Iso(actual.Time));
                Assert.Equal(Iso(expected.LocalDateTime), Iso(actual.LocalDateTime));
                Assert.Equal(Iso(expected.Instant), Iso(actual.Instant));
                Assert.Equal(Iso(expected.OffsetDateTime), Iso(actual.OffsetDateTime));
                Assert.Equal(Iso(expected.Duration), Iso(actual.Duration));
            }
        }

        // ---- What is actually stored ----

        /// <summary>
        /// Pins the JSON written to the database. Changing this format would break reading data that is already
        /// stored, so a failure here needs a deliberate decision, not just an updated expectation.
        /// </summary>
        [Fact]
        public async Task Stored_json_format()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, _) = await InsertRowsAsync(db);

            Assert.Equal("""["2019-02-01T00:00:00","2020-12-31T00:00:00"]""", await RawAsync(db, a, "[Dates]"));
            Assert.Equal("""["10:01:01.001","23:59:59.9999999"]""", await RawAsync(db, a, "[Times]"));
            Assert.Equal("""["2019-02-01T10:01:01.001","2020-12-31T23:59:59.9999999"]""", await RawAsync(db, a, "[LocalDateTimes]"));
            Assert.Equal("""["2019-02-01T10:00:00Z","2020-12-31T23:59:59.9999999Z"]""", await RawAsync(db, a, "[Instants]"));
            Assert.Equal("""["2019-02-01T10:00:00+05:30","2020-12-31T23:59:59.9999999-08:00"]""", await RawAsync(db, a, "[OffsetDateTimes]"));
            Assert.Equal("""["1:02:03.004","23:59:59.9999999"]""", await RawAsync(db, a, "[Durations]"));

            Assert.Equal("2019-02-01T00:00:00", await RawAsync(db, a, "JSON_VALUE([Json], '$.Date')"));
            Assert.Null(await RawAsync(db, a, "JSON_VALUE([Json], '$.NullableDate')"));
            Assert.Equal("10:01:01.001", await RawAsync(db, a, "JSON_VALUE([Json], '$.Time')"));
            Assert.Equal("2019-02-01T10:01:01.001", await RawAsync(db, a, "JSON_VALUE([Json], '$.LocalDateTime')"));
            Assert.Equal("2019-02-01T10:00:00Z", await RawAsync(db, a, "JSON_VALUE([Json], '$.Instant')"));
            Assert.Equal("2019-02-01T10:00:00+05:30", await RawAsync(db, a, "JSON_VALUE([Json], '$.OffsetDateTime')"));
            Assert.Equal("1:02:03.004", await RawAsync(db, a, "JSON_VALUE([Json], '$.Duration')"));
        }

        // ---- Queries that run inside SQL Server against the JSON ----

        [Fact]
        public async Task LocalDate_list_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var value = new LocalDate(2020, 12, 31);
            var threshold = new LocalDate(2018, 6, 1);
            var first = new LocalDate(2017, 6, 15);

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Dates.Contains(value))));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Dates.Any(d => d > threshold))));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.Dates[0] == first)));
        }

        [Fact]
        public async Task LocalTime_list_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var value = AlmostMidnight;
            var threshold = new LocalTime(9, 30);
            var first = new LocalTime(8, 0);

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Times.Contains(value))));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Times.Any(t => t > threshold))));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.Times[0] == first)));
        }

        [Fact]
        public async Task LocalDateTime_list_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var value = new LocalDate(2020, 12, 31).At(AlmostMidnight);
            var threshold = new LocalDateTime(2018, 6, 1, 0, 0);
            var first = new LocalDateTime(2017, 6, 15, 8, 0);

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.LocalDateTimes.Contains(value))));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.LocalDateTimes.Any(d => d > threshold))));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.LocalDateTimes[0] == first)));
        }

        [Fact]
        public async Task Instant_list_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var value = new LocalDate(2020, 12, 31).At(AlmostMidnight).InUtc().ToInstant();
            var threshold = Instant.FromUtc(2018, 6, 1, 0, 0);
            var first = Instant.FromUtc(2017, 6, 15, 8, 0);

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Instants.Contains(value))));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Instants.Any(i => i > threshold))));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.Instants[0] == first)));
        }

        [Fact]
        public async Task OffsetDateTime_list_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var value = new OffsetDateTime(new LocalDate(2020, 12, 31).At(AlmostMidnight), Offset.FromHours(-8));
            var first = new OffsetDateTime(new LocalDateTime(2017, 6, 15, 8, 0), Offset.FromHoursAndMinutes(5, 30));

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.OffsetDateTimes.Contains(value))));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.OffsetDateTimes[0] == first)));
        }

        [Fact]
        public async Task Duration_list_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var value = Duration.FromTicks(AlmostMidnight.TickOfDay);
            var threshold = Duration.FromMinutes(45);
            var first = Duration.FromMinutes(10);

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Durations.Contains(value))));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Durations.Any(d => d > threshold))));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.Durations[0] == first)));
        }

        [Fact]
        public async Task Json_column_queries()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);
            var rowA = RowA().Json;
            var date = rowA.Date;
            var time = rowA.Time;
            var localDateTime = rowA.LocalDateTime;
            var instant = rowA.Instant;
            var offsetDateTime = rowA.OffsetDateTime;
            var duration = rowA.Duration;
            var dateThreshold = new LocalDate(2018, 6, 1);
            var instantThreshold = Instant.FromUtc(2018, 6, 1, 0, 0);

            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.Date == date)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.Date > dateThreshold)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.NullableDate == null)));
            Assert.Equal([b], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.NullableDate < dateThreshold)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.Time == time)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.LocalDateTime == localDateTime)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.Instant == instant)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.Instant > instantThreshold)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.OffsetDateTime == offsetDateTime)));
            Assert.Equal([a], await IdsAsync(db.NodaTimeCollections.Where(x => x.Json.Duration == duration)));
        }

        [Fact]
        public async Task Json_column_projection()
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, b) = await InsertRowsAsync(db);

            var projected = await db.NodaTimeCollections
                .Where(x => x.Id == a || x.Id == b)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Json.Date, x.Json.NullableDate, x.Json.Time, x.Json.LocalDateTime, x.Json.Instant, x.Json.OffsetDateTime, x.Json.Duration })
                .ToListAsync();

            foreach (var (expected, actual) in new[] { RowA().Json, RowB().Json }.Zip(projected))
            {
                Assert.Equal(Iso(expected.Date), Iso(actual.Date));
                Assert.Equal(Iso(expected.NullableDate), Iso(actual.NullableDate));
                Assert.Equal(Iso(expected.Time), Iso(actual.Time));
                Assert.Equal(Iso(expected.LocalDateTime), Iso(actual.LocalDateTime));
                Assert.Equal(Iso(expected.Instant), Iso(actual.Instant));
                Assert.Equal(Iso(expected.OffsetDateTime), Iso(actual.OffsetDateTime));
                Assert.Equal(Iso(expected.Duration), Iso(actual.Duration));
            }
        }

        // ---- JSON written by something other than this library ----

        /// <summary>
        /// This library always writes Instants with "Z", but JSON written by other tools may use a numeric offset.
        /// The JSON reader used to convert those to the machine's local time, which was then taken as UTC, so this
        /// only fails on a machine whose time zone isn't UTC (CI runners are UTC).
        /// </summary>
        [Theory]
        [InlineData("2019-02-01T10:00:00Z", "2019-02-01T10:00:00Z")]
        [InlineData("2019-02-01T10:00:00+00:00", "2019-02-01T10:00:00Z")]
        [InlineData("2019-02-01T15:30:00+05:30", "2019-02-01T10:00:00Z")]
        [InlineData("2019-02-01T02:00:00-08:00", "2019-02-01T10:00:00Z")]
        [InlineData("2019-02-01T11:00:00.1234567+01:00", "2019-02-01T10:00:00.1234567Z")]
        // No offset: treated as UTC, the same as InstantValueConverter does for values read from columns.
        [InlineData("2019-02-01T10:00:00", "2019-02-01T10:00:00Z")]
        public async Task Instant_json_written_elsewhere_is_read_correctly(string stored, string expected)
        {
            using var db = CreateContext();
            using var tx = await db.Database.BeginTransactionAsync();
            var (a, _) = await InsertRowsAsync(db);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE [NodaTimeCollections] SET [Instants] = {0}, [Json] = JSON_MODIFY([Json], '$.Instant', {1}) WHERE [Id] = {2}",
                $"[\"{stored}\"]", stored, a);

            var row = await db.NodaTimeCollections.SingleAsync(x => x.Id == a);

            Assert.Equal(expected, Iso(row.Instants.Single()));
            Assert.Equal(expected, Iso(row.Json.Instant));
        }

        // ---- Helpers ----

        private RacingContext CreateContext()
            => new(new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime())
                .Options);

        private static async Task<(int A, int B)> InsertRowsAsync(RacingContext db)
        {
            var a = RowA();
            var b = RowB();
            db.NodaTimeCollections.AddRange(a, b);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return (a.Id, b.Id);
        }

        private static async Task<int[]> IdsAsync(IQueryable<NodaTimeCollections> query)
            => (await query.Select(x => x.Id).OrderBy(id => id).ToListAsync()).ToArray();

        /// <summary>Reads what is stored, bypassing EF Core's JSON reader. columnExpression is always a literal from this class.</summary>
        private static async Task<string> RawAsync(RacingContext db, int id, string columnExpression)
        {
            var sql = "SELECT " + columnExpression + " AS [Value] FROM [NodaTimeCollections] WHERE [Id] = {0}";
            return (await db.Database.SqlQueryRaw<string>(sql, id).ToListAsync()).Single();
        }

        private static void AssertList<T>(List<T> expected, List<T> actual)
            => Assert.Equal(expected.Select(v => Iso(v)), actual.Select(v => Iso(v)));
    }
}
