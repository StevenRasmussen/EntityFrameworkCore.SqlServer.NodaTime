using Microsoft.EntityFrameworkCore;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using DurationExtensions = Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions.DurationExtensions;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// list.Contains(x.NodaTimeProperty) for every NodaTime type, under each way EF Core can send a list to
    /// SQL Server (https://github.com/StevenRasmussen/EntityFrameworkCore.SqlServer.NodaTime/issues/41).
    /// </summary>
    public class ContainsQueryTests : QueryTestBase
    {
        public ContainsQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        /// <summary>
        /// "Default" leaves EF Core's default in place; the others are set with UseParameterizedCollectionMode.
        /// Parameter = one JSON parameter expanded with OPENJSON, MultipleParameters = IN (@p1, @p2, ...),
        /// Constant = IN ('literal', ...).
        /// </summary>
        private static readonly string[] ModeNames =
        [
            "Default",
            nameof(ParameterTranslationMode.Parameter),
            nameof(ParameterTranslationMode.MultipleParameters),
            nameof(ParameterTranslationMode.Constant),
        ];

        public static TheoryData<string> Modes => new(ModeNames);

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task LocalDate_Contains(string mode)
        {
            var values = new[] { new LocalDate(2019, 2, 1), new LocalDate(2019, 3, 1) };
            using var db = CreateContext(mode);

            var ids = await db.Race.Where(r => values.Contains(r.Date)).Select(r => r.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [2, 3], ids);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task LocalTime_Contains(string mode)
        {
            var values = new[] { new LocalTime(9, 1, 1, 1), new LocalTime(10, 2, 2, 2) };
            using var db = CreateContext(mode);

            var ids = await db.Race.Where(r => values.Contains(r.ScheduledStartTime)).Select(r => r.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [2, 3], ids);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task LocalDateTime_Contains(string mode)
        {
            var values = new[] { new LocalDateTime(2019, 2, 1, 9, 1, 1, 1), new LocalDateTime(2019, 3, 1, 10, 2, 2, 2) };
            using var db = CreateContext(mode);

            var ids = await db.Race.Where(r => values.Contains(r.ScheduledStart)).Select(r => r.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [2, 3], ids);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Duration_Contains(string mode)
        {
            var values = new[] { DurationExtensions.FromParts(2, 1, 1, 1), DurationExtensions.FromParts(3, 2, 2, 2) };
            using var db = CreateContext(mode);

            var ids = await db.Race.Where(r => values.Contains(r.ScheduledDuration)).Select(r => r.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [2, 3], ids);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Instant_Contains(string mode)
        {
            var values = new[] { Instant.FromUtc(2019, 2, 1, 9, 0), Instant.FromUtc(2019, 3, 1, 10, 0) };
            using var db = CreateContext(mode);

            var ids = await db.RaceResult.Where(r => values.Contains(r.StartTime)).Select(r => r.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [2, 3], ids);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task OffsetDateTime_Contains(string mode)
        {
            var values = new[]
            {
                new OffsetDateTime(new LocalDateTime(2019, 2, 1, 9, 0), Offset.FromHours(5)),
                new OffsetDateTime(new LocalDateTime(2019, 3, 1, 10, 0), Offset.FromHours(5)),
            };
            using var db = CreateContext(mode);

            var ids = await db.RaceResult.Where(r => values.Contains(r.StartTimeOffset)).Select(r => r.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [2, 3], ids);
        }

        /// <summary>
        /// The list values are converted to the column's store type, so cover the non-default store types too
        /// (SQL Server is stricter about the string formats it accepts for datetime and smalldatetime).
        /// Row 2 holds 2019-07-01 14:30 in every column; the second value matches nothing.
        /// </summary>
        public static TheoryData<string, string> NonDefaultStoreTypeCases()
        {
            var data = new TheoryData<string, string>();
            foreach (var mode in ModeNames)
                foreach (var column in new[]
                {
                    nameof(PrecisionNodaTypes.InstantAsDateTime),
                    nameof(PrecisionNodaTypes.InstantAsSmallDateTime),
                    nameof(PrecisionNodaTypes.LocalDateTimeAsDateTime),
                    nameof(PrecisionNodaTypes.LocalDateTimeAsSmallDateTime),
                    nameof(PrecisionNodaTypes.OffsetDateTimeAsDateTimeOffsetOf3),
                    nameof(PrecisionNodaTypes.LocalTimeAsTimeOf3),
                    nameof(PrecisionNodaTypes.DurationAsTimeOf3),
                })
                    data.Add(mode, column);
            return data;
        }

        [Theory]
        [MemberData(nameof(NonDefaultStoreTypeCases))]
        public async Task Contains_on_column_with_non_default_store_type(string mode, string column)
        {
            var match = new LocalDateTime(2019, 7, 1, 14, 30);
            var noMatch = new LocalDateTime(2000, 1, 1, 0, 0);
            var instants = new[] { match.InUtc().ToInstant(), noMatch.InUtc().ToInstant() };
            var localDateTimes = new[] { match, noMatch };
            var offsetDateTimes = new[] { new OffsetDateTime(match, Offset.FromHoursAndMinutes(5, 30)), new OffsetDateTime(noMatch, Offset.FromHoursAndMinutes(5, 30)) };
            var localTimes = new[] { match.TimeOfDay, new LocalTime(1, 2, 3) };
            var durations = new[] { Duration.FromTicks(match.TimeOfDay.TickOfDay), Duration.FromMinutes(1) };
            using var db = CreateContext(mode);

            IQueryable<PrecisionNodaTypes> query = column switch
            {
                nameof(PrecisionNodaTypes.InstantAsDateTime) => db.PrecisionTypes.Where(x => instants.Contains(x.InstantAsDateTime)),
                nameof(PrecisionNodaTypes.InstantAsSmallDateTime) => db.PrecisionTypes.Where(x => instants.Contains(x.InstantAsSmallDateTime)),
                nameof(PrecisionNodaTypes.LocalDateTimeAsDateTime) => db.PrecisionTypes.Where(x => localDateTimes.Contains(x.LocalDateTimeAsDateTime)),
                nameof(PrecisionNodaTypes.LocalDateTimeAsSmallDateTime) => db.PrecisionTypes.Where(x => localDateTimes.Contains(x.LocalDateTimeAsSmallDateTime)),
                nameof(PrecisionNodaTypes.OffsetDateTimeAsDateTimeOffsetOf3) => db.PrecisionTypes.Where(x => offsetDateTimes.Contains(x.OffsetDateTimeAsDateTimeOffsetOf3)),
                nameof(PrecisionNodaTypes.LocalTimeAsTimeOf3) => db.PrecisionTypes.Where(x => localTimes.Contains(x.LocalTimeAsTimeOf3)),
                nameof(PrecisionNodaTypes.DurationAsTimeOf3) => db.PrecisionTypes.Where(x => durations.Contains(x.DurationAsTimeOf3)),
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, null),
            };
            var ids = await query.Select(x => x.Id).ToListAsync();

            AssertResult(mode, db, [2], ids);
        }

        /// <summary>
        /// A Duration stored as bigint (nanoseconds) or int (seconds) (#38): the list goes as JSON numbers, or as
        /// numeric literals. See RacingContext.DurationStorageRows: row 6 holds 25 hours, row 9 -1.5s (int: -1s).
        /// </summary>
        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Duration_stored_as_bigint_Contains(string mode)
        {
            var values = new[] { Duration.FromHours(25), -Duration.FromMilliseconds(1500), Duration.FromHours(3) };
            using var db = CreateContext(mode);

            var ids = await db.DurationStorageTypes.Where(x => values.Contains(x.BigInt.Value)).Select(x => x.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [6, 9], ids, constantStart: "CAST(90000000000000 AS bigint)");
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Duration_stored_as_int_Contains(string mode)
        {
            var values = new[] { Duration.FromHours(25), Duration.FromSeconds(-1), Duration.FromHours(3) };
            using var db = CreateContext(mode);

            var ids = await db.DurationStorageTypes.Where(x => values.Contains(x.Int.Value)).Select(x => x.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [6, 9], ids, constantStart: "90000");
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Duration_stored_as_datetime2_Contains(string mode)
        {
            var values = new[] { Duration.FromHours(25), -Duration.FromMilliseconds(1500), Duration.FromHours(3) };
            using var db = CreateContext(mode);

            var ids = await db.DurationStorageTypes.Where(x => values.Contains(x.DateTime2.Value)).Select(x => x.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [6, 9], ids, constantStart: "'1900-01-02T01:00:00");
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public async Task Duration_stored_as_text_Contains(string mode)
        {
            var values = new[] { Duration.FromHours(25), -Duration.FromMilliseconds(1500), Duration.FromHours(3) };
            using var db = CreateContext(mode);

            var ids = await db.DurationStorageTypes.Where(x => values.Contains(x.Text.Value)).Select(x => x.Id).OrderBy(id => id).ToListAsync();

            AssertResult(mode, db, [6, 9], ids, constantStart: "N'00000001:01:00:00.000000000'");
        }

        [Fact]
        public async Task Contains_with_empty_list_returns_nothing()
        {
            var values = Array.Empty<LocalDate>();

            var ids = await this.Db.Race.Where(r => values.Contains(r.Date)).Select(r => r.Id).ToListAsync();

            Assert.Empty(ids);
        }

        private RacingContext CreateContext(string mode)
        {
            var options = new DbContextOptionsBuilder<RacingContext>()
                // EF Core's compiled-query cache doesn't key on the collection mode, so without a separate
                // service provider (and cache) per context, whichever mode ran first is reused for all of them.
                .EnableServiceProviderCaching(false)
                // Otherwise EF Core logs inlined constants as "?" and the Constant-mode SQL can't be checked.
                .EnableSensitiveDataLogging()
                .UseSqlServer(this.SqlConnection.ConnectionString, x =>
                {
                    x.UseNodaTime();
                    if (mode != "Default")
                        x.UseParameterizedCollectionMode(Enum.Parse<ParameterTranslationMode>(mode));
                })
                .Options;
            return new RacingContext(options);
        }

        /// <summary>
        /// Checks the rows, and that the SQL used the requested translation (so a mode can't silently fall back to another).
        /// </summary>
        private static void AssertResult(string mode, RacingContext db, int[] expectedIds, List<int> actualIds, string constantStart = "'")
        {
            Assert.Equal(expectedIds, actualIds);

            var sql = condense(db.Sql);
            switch (mode)
            {
                case nameof(ParameterTranslationMode.Parameter):
                    Assert.True(sql.Contains("OPENJSON("), sql);
                    break;
                case nameof(ParameterTranslationMode.MultipleParameters):
                    Assert.True(sql.Contains(" IN (@") && !sql.Contains("OPENJSON"), sql);
                    break;
                case nameof(ParameterTranslationMode.Constant):
                    Assert.True(sql.Contains(" IN (" + constantStart) && !sql.Contains("OPENJSON"), sql);
                    break;
            }
        }
    }
}
