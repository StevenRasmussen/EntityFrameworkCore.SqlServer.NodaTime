using Microsoft.EntityFrameworkCore;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Verifies that HasPrecision and sized/alternate column types are honoured. These paths go through
    /// RelationalTypeMapping.Clone (see SqlServerNodaTimeTypeMappingSourcePlugin), which is the API that
    /// broke between EF Core 10 and 11. No database connection is opened.
    /// </summary>
    [Trait("Category", "Offline")]
    public class PrecisionMappingTests
    {
        private static readonly RacingContext Context = new RacingContext(
            new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer("Server=offline;Database=Offline;Trusted_Connection=True", x => x.UseNodaTime())
                .Options);

        [Theory]
        [InlineData(nameof(PrecisionNodaTypes.InstantAsDateTime), "datetime")]
        [InlineData(nameof(PrecisionNodaTypes.InstantAsSmallDateTime), "smalldatetime")]
        [InlineData(nameof(PrecisionNodaTypes.InstantAsDateTime2Of0), "datetime2(0)")]
        [InlineData(nameof(PrecisionNodaTypes.InstantWithPrecision3), "datetime2(3)")]
        [InlineData(nameof(PrecisionNodaTypes.LocalDateTimeAsDateTime), "datetime")]
        [InlineData(nameof(PrecisionNodaTypes.LocalDateTimeAsSmallDateTime), "smalldatetime")]
        [InlineData(nameof(PrecisionNodaTypes.LocalDateTimeAsDateTime2Of0), "datetime2(0)")]
        [InlineData(nameof(PrecisionNodaTypes.LocalDateTimeWithPrecision3), "datetime2(3)")]
        [InlineData(nameof(PrecisionNodaTypes.OffsetDateTimeAsDateTimeOffsetOf3), "datetimeoffset(3)")]
        [InlineData(nameof(PrecisionNodaTypes.OffsetDateTimeWithPrecision2), "datetimeoffset(2)")]
        [InlineData(nameof(PrecisionNodaTypes.LocalTimeAsTimeOf3), "time(3)")]
        [InlineData(nameof(PrecisionNodaTypes.DurationAsTimeOf3), "time(3)")]
        public void Configured_column_type_or_precision_is_honoured(string propertyName, string expectedStoreType)
        {
            var property = Context.Model.FindEntityType(typeof(PrecisionNodaTypes))!.FindProperty(propertyName)!;
            var mapping = property.GetRelationalTypeMapping();

            // The NodaTime plugin must supply the mapping, not EF Core's built-in DateTime/TimeSpan mapping.
            Assert.Equal(property.ClrType, mapping.ClrType);
            Assert.Equal(expectedStoreType, mapping.StoreType);
            Assert.Contains($"[{propertyName}] {expectedStoreType} NOT NULL", Context.Database.GenerateCreateScript());
        }
    }
}
