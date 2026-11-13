using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
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
    /// ZonedDateTime, DateTimeZone and Offset properties (#22). A ZonedDateTime is stored as its OffsetDateTime (a
    /// datetimeoffset) and its zone's ID. Every query must give the same answer as NodaTime does in .NET for the values
    /// read back. The rows are in <see cref="RacingContext.ZonedEventRows"/>.
    /// </summary>
    public class ZonedDateTimeQueryTests : QueryTestBase
    {
        public ZonedDateTimeQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        private static readonly DateTimeZone Paris = DateTimeZoneProviders.Tzdb["Europe/Paris"];
        private static readonly Instant June2024 = Instant.FromUtc(2024, 6, 1, 0, 0);

        [Fact]
        public async Task Rows_round_trip()
        {
            var rows = await this.Db.ZonedEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

            Assert.Equal(RacingContext.ZonedEventRows().Select(Format), rows.Select(Format));
        }

        // The parts of a ZonedDateTime, and of the OffsetDateTime it is stored as

        public static TheoryData<string> Selectors => new(SelectorExpressions.Keys);

        private static readonly Dictionary<string, LambdaExpression> SelectorExpressions = new()
        {
            ["Start"] = (Expression<Func<ZonedEvent, ZonedDateTime>>)(x => x.Start),
            ["End"] = (Expression<Func<ZonedEvent, ZonedDateTime?>>)(x => x.End),
            ["Start.ToInstant()"] = (Expression<Func<ZonedEvent, Instant>>)(x => x.Start.ToInstant()),
            ["Start.ToOffsetDateTime()"] = (Expression<Func<ZonedEvent, OffsetDateTime>>)(x => x.Start.ToOffsetDateTime()),
            ["Start.LocalDateTime"] = (Expression<Func<ZonedEvent, LocalDateTime>>)(x => x.Start.LocalDateTime),
            ["Start.Offset"] = (Expression<Func<ZonedEvent, Offset>>)(x => x.Start.Offset),
            ["Start.Zone"] = (Expression<Func<ZonedEvent, DateTimeZone>>)(x => x.Start.Zone),
            ["Start.Zone.Id"] = (Expression<Func<ZonedEvent, string>>)(x => x.Start.Zone.Id),
            ["Start.Year"] = (Expression<Func<ZonedEvent, int>>)(x => x.Start.Year),
            ["Start.Hour"] = (Expression<Func<ZonedEvent, int>>)(x => x.Start.Hour),
            ["Start.Date"] = (Expression<Func<ZonedEvent, LocalDate>>)(x => x.Start.Date),
            ["Start.TimeOfDay"] = (Expression<Func<ZonedEvent, LocalTime>>)(x => x.Start.TimeOfDay),
            ["Booked.ToInstant()"] = (Expression<Func<ZonedEvent, Instant>>)(x => x.Booked.ToInstant()),
            ["Booked.LocalDateTime"] = (Expression<Func<ZonedEvent, LocalDateTime>>)(x => x.Booked.LocalDateTime),
            ["Booked.Offset"] = (Expression<Func<ZonedEvent, Offset>>)(x => x.Booked.Offset),
            ["HomeZone"] = (Expression<Func<ZonedEvent, DateTimeZone>>)(x => x.HomeZone),
            ["UtcOffset"] = (Expression<Func<ZonedEvent, Offset?>>)(x => x.UtcOffset),
            ["Schedule.Reminder"] = (Expression<Func<ZonedEvent, ZonedDateTime?>>)(x => x.Schedule.Reminder),
        };

        [Theory]
        [MemberData(nameof(Selectors))]
        public Task Select(string selector)
            => (Task)typeof(ZonedDateTimeQueryTests)
                .GetMethod(nameof(AssertSelectSameAsDotNet), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(SelectorExpressions[selector].ReturnType)
                .Invoke(this, [SelectorExpressions[selector]])!;

        // Filters

        [Fact]
        public Task Instant_after_a_parameter() => AssertWhereSameAsDotNet(x => x.Start.ToInstant() > June2024);

        [Fact]
        public Task Instant_before_a_parameter_written_the_other_way_round() => AssertWhereSameAsDotNet(x => June2024 > x.Start.ToInstant());

        [Fact]
        public Task Instant_equal_to_another_columns_instant() => AssertWhereSameAsDotNet(x => x.Start.ToInstant() == x.Booked.ToInstant());

        [Fact]
        public Task Instant_of_an_OffsetDateTime_column() => AssertWhereSameAsDotNet(x => x.Booked.ToInstant() >= June2024);

        [Fact]
        public Task Instant_of_a_nullable_ZonedDateTime() => AssertWhereSameAsDotNet(x => x.End != null && x.End.Value.ToInstant() > x.Start.ToInstant());

        [Fact]
        public Task Instant_inside_a_complex_type() => AssertWhereSameAsDotNet(x => x.Schedule.Reminder != null && x.Schedule.Reminder.Value.ToInstant() < x.Start.ToInstant());

        [Fact]
        public Task Equal_to_a_parameter()
        {
            var value = RacingContext.ZonedEventRows()[2].Start;
            return AssertWhereSameAsDotNet(x => x.Start == value);
        }

        [Fact]
        public Task Null() => AssertWhereSameAsDotNet(x => x.End == null);

        [Fact]
        public Task Zone() => AssertWhereSameAsDotNet(x => x.Start.Zone == Paris);

        [Fact]
        public Task Zone_Id() => AssertWhereSameAsDotNet(x => x.Start.Zone.Id == "Europe/Paris");

        [Fact]
        public Task Offset_of_the_ZonedDateTime() => AssertWhereSameAsDotNet(x => x.Start.Offset == Offset.FromHours(2));

        [Fact]
        public Task Local_date_and_time_parts() => AssertWhereSameAsDotNet(x => x.Start.Year == 2024 && x.Start.Hour == 10);

        [Fact]
        public Task DateTimeZone_property() => AssertWhereSameAsDotNet(x => x.HomeZone == Paris);

        [Fact]
        public Task Offset_property() => AssertWhereSameAsDotNet(x => x.UtcOffset > Offset.FromHours(1));

        [Fact]
        public Task Contains_zones()
        {
            var zones = new[] { Paris, DateTimeZoneProviders.Tzdb["Asia/Kathmandu"] };
            return AssertWhereSameAsDotNet(x => zones.Contains(x.HomeZone));
        }

        [Fact]
        public Task Contains_offsets()
        {
            var offsets = new[] { Offset.FromHours(-4), Offset.FromHoursAndMinutes(5, 45) };
            return AssertWhereSameAsDotNet(x => x.UtcOffset != null && offsets.Contains(x.UtcOffset.Value));
        }

        [Fact]
        public async Task OrderBy_instant()
        {
            var rows = await this.Db.ZonedEvents.AsNoTracking().ToListAsync();

            Assert.Equal(
                rows.OrderBy(x => x.Start.ToInstant()).Select(x => x.Id),
                await this.Db.ZonedEvents.OrderBy(x => x.Start.ToInstant()).Select(x => x.Id).ToListAsync());
        }

        /// <summary>
        /// A string compared with a zone's ID is sent as the column's own type, so the column needs no conversion, which
        /// would rule out using an index on it.
        /// </summary>
        [Fact]
        public void Zone_Id_parameter_has_the_column_type()
        {
            var id = "Europe/Paris";

            // EF Core sends a char column's parameters as varchar.
            Assert.Contains("DECLARE @id varchar(32) = 'Europe/Paris';", this.Db.ZonedEvents.Where(x => x.Start.Zone.Id == id).ToQueryString());
            Assert.Contains("DECLARE @id nvarchar(50) = N'Europe/Paris';", this.Db.ZonedEvents.Where(x => x.HomeZone.Id == id).ToQueryString());
        }

        /// <summary>
        /// A ZonedDateTime computed in the query has no columns. At the end of a query, EF Core evaluates its parts in .NET,
        /// from the ZonedDateTime values it reads, as it does a part with no translation.
        /// </summary>
        [Fact]
        public async Task Parts_evaluated_in_dotnet()
        {
            var rows = await this.Db.ZonedEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

            Assert.Equal(
                rows.Select(x => (x.End ?? x.Start).Hour),
                await this.Db.ZonedEvents.OrderBy(x => x.Id).Select(x => (x.End ?? x.Start).Hour).ToListAsync());
            Assert.Equal(
                rows.Select(x => x.Start.DayOfWeek),
                await this.Db.ZonedEvents.OrderBy(x => x.Id).Select(x => x.Start.DayOfWeek).ToListAsync());
        }

        /// <summary>
        /// SQL Server compares datetimeoffset values by instant, so the column itself is compared with the instant (a UTC
        /// datetime2, read as +00:00), and an index on it can be used.
        /// </summary>
        [Fact]
        public void Instant_comparison_uses_the_column_itself()
        {
            var instant = June2024;

            Assert.Contains("WHERE [z].[Start] > @instant", this.Db.ZonedEvents.Where(x => x.Start.ToInstant() > instant).ToQueryString());
            Assert.Contains("WHERE [z].[Booked] < @instant", this.Db.ZonedEvents.Where(x => instant > x.Booked.ToInstant()).ToQueryString());
        }

        [Fact]
        public async Task Update()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime()).Options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var row = await db.ZonedEvents.OrderBy(x => x.Id).FirstAsync();
            row.Start = new LocalDateTime(2030, 5, 5, 5, 5).InZoneStrictly(DateTimeZoneProviders.Tzdb["Australia/Lord_Howe"]);
            row.End = null;
            row.Schedule.Reminder = row.Start.PlusHours(-1);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.ZonedEvents.SingleAsync(x => x.Id == row.Id);

            Assert.Equal(Format(row), Format(readBack));
        }

        /// <summary>
        /// With the BCL provider, zones are the operating system's: on Windows, IDs such as "W. Europe Standard Time".
        /// The provider is part of the options, so these contexts get their own internal services.
        /// </summary>
        [Fact]
        public async Task Bcl_time_zones()
        {
            var options = new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime(n => n.UseDateTimeZoneProvider(DateTimeZoneProviders.Bcl)))
                .Options;
            using var db = new RacingContext(options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var zone = DateTimeZoneProviders.Bcl.GetSystemDefault();
            var row = new ZonedEvent { Start = new LocalDateTime(2024, 7, 1, 10, 0).InZoneLeniently(zone), HomeZone = zone, Booked = new OffsetDateTime(new LocalDateTime(2024, 7, 1, 10, 0), Offset.Zero) };
            db.ZonedEvents.Add(row);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.ZonedEvents.SingleAsync(x => x.Id == row.Id);

            Assert.Equal(zone.Id, readBack.Start.Zone.Id);
            Assert.Equal(zone.Id, readBack.HomeZone.Id);
            Assert.Equal(row.Start.ToOffsetDateTime(), readBack.Start.ToOffsetDateTime());
            var sql = "SELECT RTRIM([Start_Zone]) AS [Value] FROM [ZonedEvents] WHERE [Id] = " + row.Id;
            Assert.Equal(zone.Id, await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }

        /// <summary>
        /// A default ZonedDateTime (0001-01-01 UTC) has no zone in its field, but its Zone is UTC: it is saved, and read
        /// back, as UTC.
        /// </summary>
        [Fact]
        public async Task Default_value()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime()).Options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var row = new ZonedEvent();
            db.ZonedEvents.Add(row);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.ZonedEvents.SingleAsync(x => x.Id == row.Id);

            Assert.Equal(default(ZonedDateTime), readBack.Start);
            Assert.Equal(DateTimeZone.Utc, readBack.Start.Zone);
        }

        /// <summary>In a JSON column, a ZonedDateTime is an object with its OffsetDateTime and its Zone.</summary>
        [Fact]
        public async Task In_a_json_column()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime()).Options);
            using var transaction = await db.Database.BeginTransactionAsync();
            var when = RacingContext.ZonedEventRows()[2].Start;
            var row = new NodaTimeCollections { Json = new NodaTimeJson { When = when, MaybeWhen = null } };
            db.NodaTimeCollections.Add(row);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.NodaTimeCollections.SingleAsync(x => x.Id == row.Id);
            var sql = "SELECT JSON_QUERY([Json], '$.When') AS [Value] FROM [NodaTimeCollections] WHERE [Id] = " + row.Id;
            var json = await db.Database.SqlQueryRaw<string>(sql).SingleAsync();
            var after = when.ToInstant() - Duration.FromMinutes(1);
            var ids = await db.NodaTimeCollections.Where(x => x.Id == row.Id && x.Json.When.ToInstant() > after && x.Json.When.Zone.Id == "America/New_York").Select(x => x.Id).ToListAsync();

            Assert.Equal(when, readBack.Json.When);
            Assert.Null(readBack.Json.MaybeWhen);
            Assert.Equal("""{"Zone":"America/New_York","OffsetDateTime":"2024-11-03T01:30:00-04:00"}""", json);
            Assert.Equal([row.Id], ids);
        }

        // Helpers

        private static string Format(object value) => value switch
        {
            null => "null",
            ZonedEvent row => $"{Format(row.Start)} | {Format(row.End)} | {Format(row.HomeZone)} | {Format(row.UtcOffset)} | {Format(row.Booked)} | {Format(row.Schedule.Reminder)}",
            ZonedDateTime zoned => ZonedDateTimePattern.ExtendedFormatOnlyIso.Format(zoned),
            DateTimeZone zone => zone.Id,
            Offset offset => offset.ToString(),
            Instant or LocalDateTime or OffsetDateTime or LocalDate or LocalTime or Duration => Iso(value),
            _ => value.ToString(),
        };

        private async Task AssertSelectSameAsDotNet<T>(Expression<Func<ZonedEvent, T>> selector)
        {
            var rows = await this.Db.ZonedEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            var compute = selector.Compile();
            var expected = rows.Select(row => $"{row.Id}: {Format(compute(row))}").ToList();

            var actual = await this.Db.ZonedEvents.OrderBy(x => x.Id).Select(selector).ToListAsync();
            var actualValues = rows.Zip(actual, (row, value) => $"{row.Id}: {Format(value)}").ToList();

            Assert.Equal(expected, actualValues);

            // EF Core runs a final projection it can't translate in .NET instead, which would test no SQL: then the SELECT
            // list is just the columns it needs. A whole ZonedDateTime, DateTimeZone or Offset, or a zone's ID, really is
            // just columns, though.
            var sql = condense(this.Db.Sql);
            var selectList = sql.Substring("SELECT ".Length, sql.IndexOf(" FROM ", StringComparison.Ordinal) - "SELECT ".Length);
            var justColumns = Regex.IsMatch(selectList, @"^\[z\]\.\[\w+\](, \[z\]\.\[\w+\])*$");
            var expectsJustColumns = typeof(T) == typeof(ZonedDateTime) || typeof(T) == typeof(ZonedDateTime?) || typeof(T) == typeof(OffsetDateTime)
                || typeof(T) == typeof(DateTimeZone) || typeof(T) == typeof(Offset?) || Regex.IsMatch(selectList, @"^\[z\]\.\[\w+_Zone\]$");
            Assert.True(!justColumns || expectsJustColumns, $"The projection was evaluated on the client: {sql}");
        }

        private async Task AssertWhereSameAsDotNet(Expression<Func<ZonedEvent, bool>> predicate)
        {
            var rows = await this.Db.ZonedEvents.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
            var expected = rows.Where(predicate.Compile()).Select(x => x.Id).ToList();

            var actual = await this.Db.ZonedEvents.Where(predicate).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

            Assert.Equal(expected, actual);
        }
    }
}
