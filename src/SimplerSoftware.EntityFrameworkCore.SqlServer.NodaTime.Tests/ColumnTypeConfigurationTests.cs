using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;
using System.Linq;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// What happens when a NodaTime property is given an explicit column type (HasColumnType / [Column(TypeName)]).
    /// Each test builds its own model, without a database.
    /// </summary>
    [Trait("Category", "Offline")]
    public class ColumnTypeConfigurationTests
    {
        public class Entity
        {
            public int Id { get; set; }

            public Duration Duration { get; set; }

            public Instant Instant { get; set; }

            public LocalDate Date { get; set; }

            public LocalDateTime LocalDateTime { get; set; }
        }

        private sealed class Context(Action<ModelBuilder> configure) : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder options)
                => options
                    .UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime())
                    // Each test configures the model differently; without this, EF Core would reuse the first model it built.
                    .EnableServiceProviderCaching(false);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Entity>();
                configure(modelBuilder);
            }
        }

        private static RelationalTypeMapping MappingOf(string property, string columnType, int? precision = null)
        {
            using var db = new Context(b =>
            {
                var propertyBuilder = b.Entity<Entity>().Property(property).HasColumnType(columnType);
                if (precision.HasValue)
                    propertyBuilder.HasPrecision(precision.Value);
            });
            return db.Model.FindEntityType(typeof(Entity))!.FindProperty(property)!.GetRelationalTypeMapping();
        }

        /// <summary>
        /// These used to be silently ignored: the property kept its default mapping (e.g. time for a Duration) while
        /// the column was created with the configured type.
        /// </summary>
        [Theory]
        [InlineData(nameof(Entity.Duration), "smallint")]
        [InlineData(nameof(Entity.Duration), "decimal(20, 0)")]
        [InlineData(nameof(Entity.Duration), "uniqueidentifier")]
        // Text shorter than the longest stored value (28 characters) would fail when saving, perhaps only for a negative value.
        [InlineData(nameof(Entity.Duration), "nvarchar(27)")]
        [InlineData(nameof(Entity.Duration), "char(10)")]
        [InlineData(nameof(Entity.Duration), "datetime")]
        [InlineData(nameof(Entity.Instant), "nvarchar(40)")]
        [InlineData(nameof(Entity.Instant), "BIGINT")]
        [InlineData(nameof(Entity.Date), "int")]
        [InlineData(nameof(Entity.Date), "national character varying(20)")]
        public void Unsupported_column_type_is_reported(string property, string columnType)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => MappingOf(property, columnType));

            Assert.Contains($"could not be mapped to the database type '{columnType}'", exception.Message);
        }

        /// <summary>
        /// A name that isn't a SQL Server built-in type may be a user-defined alias type (CREATE TYPE BusinessDate FROM date),
        /// so it keeps the default mapping, as before.
        /// </summary>
        [Theory]
        [InlineData(nameof(Entity.Date), "BusinessDate", "date")]
        [InlineData(nameof(Entity.Instant), "dbo.UtcTimestamp", "datetime2")]
        public void Alias_type_keeps_the_default_mapping(string property, string columnType, string expectedStoreType)
        {
            var mapping = MappingOf(property, columnType);

            Assert.Equal(typeof(Entity).GetProperty(property)!.PropertyType, mapping.ClrType);
            Assert.Equal(expectedStoreType, mapping.StoreType);
        }

        [Theory]
        [InlineData(nameof(Entity.Instant), "DATETIME2", "datetime2")]
        [InlineData(nameof(Entity.Instant), "DateTime", "datetime")]
        [InlineData(nameof(Entity.Duration), "TIME(3)", "time(3)")]
        [InlineData(nameof(Entity.Date), "Date", "date")]
        public void Column_type_names_are_not_case_sensitive(string property, string columnType, string expectedStoreType)
        {
            var mapping = MappingOf(property, columnType);

            Assert.Equal(typeof(Entity).GetProperty(property)!.PropertyType, mapping.ClrType);
            Assert.Equal(expectedStoreType, mapping.StoreType, ignoreCase: true);
        }

        /// <summary>
        /// HasPrecision applies on top of an explicit column type, as EF Core does for DateTime
        /// (HasColumnType("datetime2").HasPrecision(3) gives a datetime2(3) column). The precision used to be dropped.
        /// </summary>
        [Theory]
        [InlineData(nameof(Entity.Instant), "datetime2", "datetime2(3)")]
        [InlineData(nameof(Entity.Instant), "DATETIME2", "datetime2(3)")]
        [InlineData(nameof(Entity.Duration), "time", "time(3)")]
        [InlineData(nameof(Entity.Duration), "datetime2", "datetime2(3)")]
        // These take no precision; datetime(3) or smalldatetime(3) would be invalid SQL.
        [InlineData(nameof(Entity.Date), "date", "date")]
        [InlineData(nameof(Entity.Instant), "datetime", "datetime")]
        [InlineData(nameof(Entity.LocalDateTime), "smalldatetime", "smalldatetime")]
        public void Precision_applies_to_an_explicit_column_type(string property, string columnType, string expectedStoreType)
        {
            var mapping = MappingOf(property, columnType, precision: 3);

            Assert.Equal(expectedStoreType, mapping.StoreType, ignoreCase: true);
        }

        [Fact]
        public void Precision_is_not_kept_for_a_type_that_takes_none()
        {
            Assert.Null(MappingOf(nameof(Entity.Instant), "datetime", precision: 3).Precision);
        }

        /// <summary>A space before the size, as in "datetime2 (3)", is still datetime2.</summary>
        [Fact]
        public void Column_type_with_a_space_before_the_size_is_recognized()
        {
            var mapping = MappingOf(nameof(Entity.Instant), "datetime2 (3)");

            Assert.Equal(typeof(Instant), mapping.ClrType);
            Assert.StartsWith("datetime2", mapping.StoreType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Literals must use the format of the column's type however it is spelled; datetime can't take 7 fractional digits.
        /// </summary>
        [Theory]
        [InlineData("DATETIME", "'2019-01-01T08:30:00.000'")]
        [InlineData("SmallDateTime", "'2019-01-01T08:30:00'")]
        public void Literals_match_the_column_type_whatever_its_case(string columnType, string expectedLiteral)
        {
            var mapping = MappingOf(nameof(Entity.Instant), columnType, precision: 3);

            Assert.Equal(expectedLiteral, mapping.GenerateSqlLiteral(Instant.FromUtc(2019, 1, 1, 8, 30)));
        }

        /// <summary>A time column spelled "time (3)" is still a time column for Duration.Days.</summary>
        [Fact]
        public void Duration_Days_is_translated_for_a_time_column_with_a_space_before_the_size()
        {
            using var db = new Context(b => b.Entity<Entity>().Property(x => x.Duration).HasColumnType("time (3)"));

            var sql = db.Set<Entity>().Where(x => x.Duration.Days == 0).ToQueryString();

            Assert.Contains("DATEDIFF(day, '19000101', [e].[Duration])", sql);
        }

        /// <summary>
        /// Duration.Days is translated for a time column only. With a custom value converter to another type it must
        /// not be translated, rather than give the days since 1900.
        /// </summary>
        [Fact]
        public void Duration_Days_is_not_translated_for_a_non_time_column()
        {
            using var db = new Context(b => b.Entity<Entity>().Property(x => x.Duration)
                .HasConversion(new ValueConverter<Duration, long>(d => d.BclCompatibleTicks, ticks => Duration.FromTicks(ticks))));

            var exception = Assert.Throws<InvalidOperationException>(
                () => db.Set<Entity>().Where(x => x.Duration.Days == 1).ToQueryString());

            Assert.Contains("could not be translated", exception.Message);
        }
    }
}
