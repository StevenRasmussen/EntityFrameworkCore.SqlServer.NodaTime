using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using NodaTime;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Queries against nullable NodaTime columns. Seed data (see RacingContext): TypeResults row 1 has every
    /// nullable column populated, row 2 has every nullable column null.
    /// </summary>
    public class NullableQueryTests : QueryTestBase
    {
        private const string CountStatement = "SELECT COUNT(*) FROM [TypeResults] AS [t]";

        public NullableQueryTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture) { }

        [Fact]
        public async Task Nullable_columns_can_be_compared_to_null()
        {
            var count = await this.Db.TypeResults.CountAsync(t =>
                t.DurationNull == null && t.InstantNull == null && t.LocalDateNull == null
                && t.LocalDateTimeNull == null && t.LocalTimeNull == null && t.OffsetDateTimeNull == null);

            Assert.Equal(
                condense($"{CountStatement} WHERE [t].[DurationNull] IS NULL AND [t].[InstantNull] IS NULL AND [t].[LocalDateNull] IS NULL AND [t].[LocalDateTimeNull] IS NULL AND [t].[LocalTimeNull] IS NULL AND [t].[OffsetDateTimeNull] IS NULL"),
                condense(this.Db.Sql));

            Assert.Equal(1, count);
        }

        // EF Core's C# null semantics: a part of a NULL value is NULL, and NULL != x is true, so row 2 (all NULL)
        // matches. EF Core does the same for DateTime?.Value.Year. These SQL functions used to be marked as never
        // NULL, which left row 2 out.

        [Fact]
        public async Task Nullable_member_not_equal_includes_null()
        {
            var ids = await this.Db.TypeResults.Where(t => t.LocalDateNull.Value.Year != 2000).Select(t => t.Id).ToListAsync();

            Assert.Equal(
                condense("SELECT [t].[Id] FROM [TypeResults] AS [t] WHERE DATEPART(year, [t].[LocalDateNull]) <> 2000 OR [t].[LocalDateNull] IS NULL"),
                condense(this.Db.Sql));
            Assert.Equal([2], ids);
        }

        [Fact]
        public async Task Nullable_method_not_equal_includes_null()
        {
            var ids = await this.Db.TypeResults.Where(t => t.LocalDateNull.Value.PlusDays(1) != new LocalDate(2000, 3, 1)).Select(t => t.Id).ToListAsync();

            Assert.Equal(
                condense("SELECT [t].[Id] FROM [TypeResults] AS [t] WHERE DATEADD(day, CAST(1 AS int), [t].[LocalDateNull]) <> '2000-03-01' OR [t].[LocalDateNull] IS NULL"),
                condense(this.Db.Sql));
            Assert.Equal([2], ids);
        }

        [Fact]
        public async Task Nullable_DateDiff_not_equal_includes_null()
        {
            var ids = await this.Db.TypeResults.Where(t => EF.Functions.DateDiffDay(t.LocalDateNull, new LocalDate(2000, 3, 1)) != 1).Select(t => t.Id).ToListAsync();

            Assert.Equal(
                condense("SELECT [t].[Id] FROM [TypeResults] AS [t] WHERE DATEDIFF(DAY, [t].[LocalDateNull], '2000-03-01') <> 1 OR [t].[LocalDateNull] IS NULL"),
                condense(this.Db.Sql));
            Assert.Equal([2], ids);
        }

        [Fact]
        public async Task Nullable_Duration_member()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.DurationNull.Value.Minutes == 30);

            Assert.Equal(condense($"{CountStatement} WHERE DATEPART(minute, [t].[DurationNull]) = 30"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_Duration_method()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.DurationNull.Value.PlusHours(1) == Duration.FromMinutes(150));

            Assert.Equal(condense($"{CountStatement} WHERE DATEDIFF_BIG(nanosecond, '19000101', [t].[DurationNull]) + CAST(1.0E0 * 3600000000000.0E0 AS bigint) = CAST(9000000000000 AS bigint)"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_Instant_member()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.InstantNull.Value.Year() == 2020);

            Assert.Equal(condense($"{CountStatement} WHERE DATEPART(year, [t].[InstantNull]) = 2020"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_Instant_method()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.InstantNull.Value.PlusDays(1) == Instant.FromUtc(2020, 3, 1, 12, 30));

            Assert.Equal(condense($"{CountStatement} WHERE DATEADD(day, CAST(1.0E0 AS int), [t].[InstantNull]) = '2020-03-01T12:30:00.0000000Z'"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_LocalDate_member()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.LocalDateNull.Value.Year == 2000);

            Assert.Equal(condense($"{CountStatement} WHERE DATEPART(year, [t].[LocalDateNull]) = 2000"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_LocalDate_method()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.LocalDateNull.Value.PlusDays(1) == new LocalDate(2000, 3, 1));

            Assert.Equal(condense($"{CountStatement} WHERE DATEADD(day, CAST(1 AS int), [t].[LocalDateNull]) = '2000-03-01'"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_LocalDateTime_member()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.LocalDateTimeNull.Value.Hour == 12);

            Assert.Equal(condense($"{CountStatement} WHERE DATEPART(hour, [t].[LocalDateTimeNull]) = 12"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_LocalDateTime_method()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.LocalDateTimeNull.Value.PlusDays(1) == new LocalDateTime(2020, 3, 1, 12, 30));

            Assert.Equal(condense($"{CountStatement} WHERE DATEADD(day, CAST(1 AS int), [t].[LocalDateTimeNull]) = '2020-03-01T12:30:00.0000000'"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_LocalTime_member()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.LocalTimeNull.Value.Minute == 30);

            Assert.Equal(condense($"{CountStatement} WHERE DATEPART(minute, [t].[LocalTimeNull]) = 30"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_LocalTime_method()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.LocalTimeNull.Value.PlusHours(1) == new LocalTime(13, 30));

            Assert.Equal(condense($"{CountStatement} WHERE DATEADD(hour, CAST(CAST(1 AS bigint) AS int), [t].[LocalTimeNull]) = '13:30:00'"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_OffsetDateTime_member()
        {
            var count = await this.Db.TypeResults.CountAsync(t => t.OffsetDateTimeNull.Value.Day == 29);

            Assert.Equal(condense($"{CountStatement} WHERE DATEPART(day, [t].[OffsetDateTimeNull]) = 29"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Nullable_OffsetDateTime_method()
        {
            var expected = new OffsetDateTime(new LocalDateTime(2020, 2, 29, 13, 30), Offset.FromHoursAndMinutes(5, 30));
            var count = await this.Db.TypeResults.CountAsync(t => t.OffsetDateTimeNull.Value.PlusHours(1) == expected);

            Assert.Equal(condense($"{CountStatement} WHERE DATEADD(hour, CAST(1 AS int), [t].[OffsetDateTimeNull]) = @expected"), condense(this.Db.Sql));
            Assert.Equal(1, count);
        }
    }
}
