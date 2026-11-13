using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;
using System.Linq;
using Xunit;
using DurationExtensions = Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions.DurationExtensions;
using SqlServerNodaTimeDbFunctionsExtensions = Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions.SqlServerNodaTimeDbFunctionsExtensions;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Storing a Duration as bigint (nanoseconds) or int (seconds) instead of time (#38): the mappings, and the shape
    /// of the SQL. Without a database; <see cref="DurationStorageQueryTests"/> checks the results.
    /// </summary>
    [Trait("Category", "Offline")]
    public class DurationStorageMappingTests
    {
        public class Entity
        {
            public int Id { get; set; }

            public Duration Time { get; set; }

            public Duration OtherTime { get; set; }

            public Duration? BigInt { get; set; }

            public Duration? Int { get; set; }

            public Duration? DateTime2 { get; set; }

            /// <summary>The user's own converter, which the library leaves alone.</summary>
            public Duration Ticks { get; set; }
        }

        private sealed class Context(Action<ModelConfigurationBuilder> conventions = null, bool throwOnTruncation = false) : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder options)
            {
                options
                    .UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime())
                    .EnableServiceProviderCaching(false);

                // Turns the warning into an exception, so a test can see whether it was logged.
                if (throwOnTruncation)
                    options.ConfigureWarnings(w => w.Throw(SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds));
            }

            protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                => conventions?.Invoke(configurationBuilder);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
                => modelBuilder.Entity<Entity>(entity =>
                {
                    if (conventions == null)
                    {
                        entity.Property(x => x.BigInt).HasColumnType("bigint");
                        entity.Property(x => x.Int).HasColumnType("int");
                        entity.Property(x => x.DateTime2).HasColumnType("datetime2");
                    }
                    entity.Property(x => x.Ticks).HasConversion(new ValueConverter<Duration, long>(d => d.BclCompatibleTicks, t => Duration.FromTicks(t)));
                });
        }

        private static RelationalTypeMapping MappingOf(DbContext db, string property)
            => db.Model.FindEntityType(typeof(Entity))!.FindProperty(property)!.GetRelationalTypeMapping();

        private static RelationalTypeMapping MappingFor(string columnType)
        {
            using var db = new Context();
            return db.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(Duration), columnType);
        }

        // Mappings

        [Theory]
        [InlineData("bigint", typeof(DurationBigIntTypeMapping), "bigint")]
        [InlineData("BIGINT", typeof(DurationBigIntTypeMapping), "bigint")]
        [InlineData("int", typeof(DurationIntTypeMapping), "int")]
        [InlineData("Int", typeof(DurationIntTypeMapping), "int")]
        [InlineData("integer", typeof(DurationIntTypeMapping), "int")]
        [InlineData("time", typeof(DurationTypeMapping), "time")]
        [InlineData("datetime2", typeof(DurationDateTime2TypeMapping), "datetime2")]
        [InlineData("DATETIME2", typeof(DurationDateTime2TypeMapping), "datetime2")]
        [InlineData("datetime2(3)", typeof(DurationDateTime2TypeMapping), "datetime2(3)")]
        public void Column_type_chooses_the_storage(string columnType, Type expectedMappingType, string expectedStoreType)
        {
            var mapping = MappingFor(columnType);

            Assert.IsType(expectedMappingType, mapping);
            Assert.Equal(expectedStoreType, mapping.StoreType, ignoreCase: true);
        }

        [Fact]
        public void Time_is_still_the_default()
        {
            using var db = new Context();

            Assert.IsType<DurationTypeMapping>(MappingOf(db, nameof(Entity.Time)));
            Assert.Equal("bigint", MappingOf(db, nameof(Entity.BigInt)).StoreType);
            Assert.Equal("int", MappingOf(db, nameof(Entity.Int)).StoreType);
        }

        /// <summary>EF Core's own way of setting the column type of every Duration, so the library needs no option for it.</summary>
        [Fact]
        public void ConfigureConventions_sets_every_Duration()
        {
            using var db = new Context(c => c.Properties<Duration>().HaveColumnType("bigint"));

            Assert.IsType<DurationBigIntTypeMapping>(MappingOf(db, nameof(Entity.Time)));
            Assert.IsType<DurationBigIntTypeMapping>(MappingOf(db, nameof(Entity.BigInt)));
        }

        [Fact]
        public void Precision_doesnt_apply_to_bigint()
        {
            using var db = new Context();
            var mapping = db.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(Duration), "bigint", precision: 3);

            Assert.Equal("bigint", mapping.StoreType);
        }

        // Values

        /// <summary>A literal outside the int range must be cast: SQL Server would type it as numeric, not bigint.</summary>
        [Theory]
        [InlineData("bigint", 25 * 3600, "CAST(90000000000000 AS bigint)")]
        [InlineData("bigint", -1.5, "CAST(-1500000000 AS bigint)")]
        [InlineData("int", 25 * 3600, "90000")]
        [InlineData("int", 1.7, "1")]
        [InlineData("int", -1.7, "-1")]
        [InlineData("datetime2", 25 * 3600, "'1900-01-02T01:00:00.0000000'")]
        [InlineData("datetime2", -3600.5, "'1899-12-31T22:59:59.5000000'")]
        public void Literal(string columnType, double seconds, string expected)
        {
            Assert.Equal(expected, MappingFor(columnType).GenerateSqlLiteral(Duration.FromSeconds(seconds)));
        }

        /// <summary>int holds whole seconds; a fraction is truncated toward zero, as SQL Server's integer division does.</summary>
        [Theory]
        [InlineData(1700, 1)]
        [InlineData(-1700, -1)]
        [InlineData(999, 0)]
        [InlineData(-999, 0)]
        public void Int_truncates_toward_zero(long milliseconds, int expectedSeconds)
        {
            Assert.Equal(expectedSeconds, DurationSecondsValueConverter.toProvider(Duration.FromMilliseconds(milliseconds)));
        }

        [Fact]
        public void Int_out_of_range_throws()
        {
            Assert.Equal(int.MaxValue, DurationSecondsValueConverter.toProvider(Duration.FromSeconds(int.MaxValue)));
            Assert.Contains("stored as int", Assert.Throws<OverflowException>(() => DurationSecondsValueConverter.toProvider(Duration.FromSeconds(int.MaxValue + 1L))).Message);
            Assert.Throws<OverflowException>(() => DurationSecondsValueConverter.toProvider(Duration.FromDays(-25_000)));
        }

        /// <summary>A bigint of nanoseconds holds about ±292 years.</summary>
        [Fact]
        public void BigInt_out_of_range_throws()
        {
            Assert.Equal(long.MaxValue, DurationNanosecondsValueConverter.toProvider(Duration.FromNanoseconds(long.MaxValue)));
            Assert.Contains("stored as bigint", Assert.Throws<OverflowException>(() => DurationNanosecondsValueConverter.toProvider(Duration.FromDays(106_752))).Message);
            Assert.Throws<OverflowException>(() => DurationNanosecondsValueConverter.toProvider(Duration.FromDays(-106_752)));
        }

        /// <summary>Used for lists of Durations (e.g. list.Contains(x.BigInt)) and JSON columns: numbers, not time strings.</summary>
        /// <summary>
        /// datetime2 holds the offset from 1900-01-01, from 0001-01-01 to 9999-12-31: about -1899 to +8099 years.
        /// Sub-100ns digits are truncated toward zero, as for time.
        /// </summary>
        [Fact]
        public void DateTime2_range_and_truncation()
        {
            Assert.Equal(new DateTime(1900, 1, 2, 1, 0, 0), DurationDateTime2ValueConverter.toProvider(Duration.FromHours(25)));
            Assert.Equal(new DateTime(1899, 12, 31, 23, 59, 59).AddTicks(9_999_999), DurationDateTime2ValueConverter.toProvider(-Duration.FromNanoseconds(150)));
            Assert.Equal(Duration.FromDays(-693_595), DurationDateTime2ValueConverter.fromProvider(DateTime.MinValue));
            var tooLong = Assert.Throws<ArgumentOutOfRangeException>(() => DurationDateTime2ValueConverter.toProvider(Duration.FromDays(-693_596)));
            Assert.Contains("stored as datetime2", tooLong.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => DurationDateTime2ValueConverter.toProvider(Duration.FromDays(2_958_465)));
        }

        [Theory]
        [InlineData("bigint", "90000000000000")]
        [InlineData("int", "90000")]
        [InlineData("datetime2", "\"1900-01-02T01:00:00\"")]
        public void Json(string columnType, string expectedJson)
        {
            var readerWriter = MappingFor(columnType).JsonValueReaderWriter!;

            Assert.Equal(expectedJson, readerWriter.ToJsonString(Duration.FromHours(25)));
            Assert.Equal(Duration.FromHours(25), readerWriter.FromJsonString(expectedJson));
        }

        // SQL

        private static string Sql<T>(Func<IQueryable<Entity>, IQueryable<T>> query)
        {
            using var db = new Context();
            return query(db.Set<Entity>()).ToQueryString();
        }

        /// <summary>Components of a datetime2 count ticks, which a bigint holds for the whole datetime2 range.</summary>
        [Fact]
        public void Hours_of_datetime2_counts_ticks()
            => Assert.Contains(
                "CAST(((DATEDIFF_BIG(microsecond, '19000101', [e].[DateTime2]) * 10 + (DATEPART(nanosecond, [e].[DateTime2]) / 100) % 10) / CAST(36000000000 AS bigint)) % 24 AS int)",
                Sql(q => q.Select(x => x.DateTime2.Value.Hours)));

        /// <summary>A time and a datetime2 compare as a datetime2, so an index on the datetime2 can still be used.</summary>
        [Fact]
        public void Comparing_time_with_datetime2_casts_the_time()
            => Assert.Contains("WHERE CAST([e].[Time] AS datetime2) < [e].[DateTime2]", Sql(q => q.Where(x => x.Time < x.DateTime2).Select(x => x.Id)));

        /// <summary>An int always fits a datetime2, so they compare as datetime2s rather than nanoseconds, which could overflow.</summary>
        [Fact]
        public void Comparing_int_with_datetime2_converts_the_int()
            => Assert.Contains(
                "WHERE DATEADD(second, [e].[Int], CAST('19000101' AS datetime2)) = [e].[DateTime2]",
                Sql(q => q.Where(x => x.Int == x.DateTime2).Select(x => x.Id)));

        [Fact]
        public void Hours_of_bigint()
            => Assert.Contains("CAST(([e].[BigInt] / CAST(3600000000000 AS bigint)) % 24 AS int)", Sql(q => q.Select(x => x.BigInt.Value.Hours)));

        [Fact]
        public void Days_of_int()
            => Assert.Contains("SELECT [e].[Int] / 86400", Sql(q => q.Select(x => x.Int.Value.Days)));

        /// <summary>DATEADD on a time wraps around at 24 hours; nanoseconds in a bigint don't.</summary>
        [Fact]
        public void PlusHours_of_time_is_done_in_nanoseconds()
            => Assert.Contains(
                "DATEDIFF_BIG(nanosecond, '19000101', [e].[Time]) + CAST(1.5E0 * 3600000000000.0E0 AS bigint)",
                Sql(q => q.Select(x => DurationExtensions.PlusHours(x.Time, 1.5))));

        [Fact]
        public void Comparing_the_same_storage_is_unchanged()
            => Assert.Contains("WHERE [e].[Time] > [e].[OtherTime]", Sql(q => q.Where(x => x.Time > x.OtherTime).Select(x => x.Id)));

        [Fact]
        public void Comparing_time_with_bigint_converts_the_time()
            => Assert.Contains(
                "WHERE DATEDIFF_BIG(nanosecond, '19000101', [e].[Time]) < [e].[BigInt]",
                Sql(q => q.Where(x => x.Time < x.BigInt).Select(x => x.Id)));

        [Fact]
        public void Conditional_between_the_same_storage_is_unchanged()
        {
            var sql = Sql(q => q.Select(x => x.Id > 1 ? x.Time : x.OtherTime));

            Assert.Contains("THEN [e].[Time]", sql);
            Assert.Contains("ELSE [e].[OtherTime]", sql);
            Assert.DoesNotContain("DATEDIFF_BIG", sql);
        }

        [Fact]
        public void Coalesce_of_int_and_bigint_converts_the_int()
            => Assert.Contains(
                "COALESCE(CAST([e].[Int] AS bigint) * 1000000000, [e].[BigInt])",
                Sql(q => q.Select(x => x.Int ?? x.BigInt)));

        // The warning for a value truncated to whole seconds

        private static string SqlThrowingOnTruncation<T>(Func<IQueryable<Entity>, IQueryable<T>> query)
        {
            using var db = new Context(throwOnTruncation: true);
            return query(db.Set<Entity>()).ToQueryString();
        }

        private static void AssertTruncationWarning<T>(Func<IQueryable<Entity>, IQueryable<T>> query)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => SqlThrowingOnTruncation(query));

            Assert.Contains(SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds.Name, exception.Message);
            Assert.Contains("stored as whole seconds (int)", exception.Message);
        }

        /// <summary>The parameter's value isn't known when the query is compiled, so it may have a fraction.</summary>
        [Fact]
        public void Comparing_int_with_a_parameter_warns()
        {
            var limit = Duration.FromSeconds(1);
            AssertTruncationWarning(q => q.Where(x => x.Int < limit).Select(x => x.Id));
        }

        [Fact]
        public void Comparing_int_with_a_constant_with_a_fraction_warns()
            => AssertTruncationWarning(q => q.Where(x => x.Int == Duration.FromMilliseconds(1500)).Select(x => x.Id));

        [Fact]
        public void Coalesce_of_int_and_a_parameter_warns()
        {
            var fallback = Duration.FromSeconds(1);
            AssertTruncationWarning(q => q.Select(x => x.Int ?? fallback));
        }

        /// <summary>Only an int column with a parameter or a constant with a fraction loses anything.</summary>
        [Fact]
        public void No_warning_when_nothing_is_truncated()
        {
            var limit = Duration.FromMilliseconds(1500);

            SqlThrowingOnTruncation(q => q.Where(x => x.Int < Duration.FromSeconds(2)).Select(x => x.Id));
            SqlThrowingOnTruncation(q => q.Where(x => x.BigInt < limit).Select(x => x.Id));
            SqlThrowingOnTruncation(q => q.Where(x => x.Time < limit).Select(x => x.Id));
            SqlThrowingOnTruncation(q => q.Where(x => x.Int < x.BigInt).Select(x => x.Id));
            // Arithmetic is done in nanoseconds, so the parameter keeps its fraction.
            SqlThrowingOnTruncation(q => q.Select(x => x.Int + limit));
            SqlThrowingOnTruncation(q => q.Select(x => SqlServerNodaTimeDbFunctionsExtensions.DateDiffBigMillisecond(EF.Functions, limit, x.Int)));
        }

        [Fact]
        public void Comparing_with_null_is_left_to_EF_Core()
            => Assert.Contains("WHERE [e].[BigInt] IS NULL", Sql(q => q.Where(x => x.BigInt == null).Select(x => x.Id)));

        /// <summary>In int, a sum could overflow past about 68 years, and a fraction of a second added to it would be lost.</summary>
        [Fact]
        public void Adding_two_ints_is_done_in_nanoseconds()
            => Assert.Contains(
                "SELECT CAST([e].[Int] AS bigint) * 1000000000 + CAST([e].[Int] AS bigint) * 1000000000",
                Sql(q => q.Select(x => x.Int + x.Int)));

        [Fact]
        public void Adding_two_bigints_is_plain_addition()
            => Assert.Contains("SELECT [e].[BigInt] + [e].[BigInt]", Sql(q => q.Select(x => x.BigInt + x.BigInt)));

        /// <summary>The ratio of two Durations is a double, which integer division in SQL would get wrong.</summary>
        [Fact]
        public void Dividing_two_durations_is_not_translated()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => Sql(q => q.Where(x => x.BigInt / x.BigInt > 1.5).Select(x => x.Id)));

            Assert.Contains("could not be translated", exception.Message);
        }

        /// <summary>A Duration with the user's own converter is translated as it was before #38.</summary>
        [Fact]
        public void Own_converter_is_left_alone()
        {
            Assert.Contains("SELECT [e].[Ticks] + [e].[Ticks]", Sql(q => q.Select(x => x.Ticks + x.Ticks)));
            Assert.Contains("DATEADD(hour, CAST(1.0E0 AS int), [e].[Ticks])", Sql(q => q.Select(x => DurationExtensions.PlusHours(x.Ticks, 1))));
        }
    }
}
