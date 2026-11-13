using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using NodaTime;
using System.Linq;
using System.Reflection;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata
{
    /// <summary>
    /// Maps every <see cref="Period"/> property, on an entity type or a complex type, as a complex type over its ten units
    /// (#3), stored in a JSON column named after the property: {"Days":1,"Hours":-1,"Minutes":90,...}. Each unit is kept
    /// as it is, sign and all, as Period's equality needs. Inside a JSON column already, it is a JSON object there. EF Core
    /// creates a Period with its constructor that takes the ten units. The application's own mapping takes precedence,
    /// as for a ZonedDateTime.
    /// </summary>
    internal sealed class PeriodConvention : IEntityTypeAddedConvention, IComplexPropertyAddedConvention, IModelFinalizingConvention
    {
        /// <summary>A Period's units, in the order NodaTime adds them.</summary>
        internal static readonly PropertyInfo[] Units = new[]
        {
            nameof(Period.Years), nameof(Period.Months), nameof(Period.Weeks), nameof(Period.Days), nameof(Period.Hours),
            nameof(Period.Minutes), nameof(Period.Seconds), nameof(Period.Milliseconds), nameof(Period.Ticks), nameof(Period.Nanoseconds),
        }.Select(name => typeof(Period).GetProperty(name)).ToArray();

        private readonly IMemberClassifier _memberClassifier;

        public PeriodConvention(IMemberClassifier memberClassifier)
        {
            _memberClassifier = memberClassifier;
        }

        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
            => AddPeriodProperties(entityTypeBuilder);

        public void ProcessComplexPropertyAdded(IConventionComplexPropertyBuilder propertyBuilder, IConventionContext<IConventionComplexPropertyBuilder> context)
        {
            var complexType = propertyBuilder.Metadata.ComplexType;
            if (complexType.ClrType != typeof(Period))
                AddPeriodProperties(complexType.Builder);
        }

        private void AddPeriodProperties(IConventionTypeBaseBuilder typeBuilder)
        {
            var type = typeBuilder.Metadata;

            // IgnoreAny<Period>() in ConfigureConventions.
            if (type.Model.IsIgnoredType(typeof(Period)))
                return;

            foreach (var property in NodaTimeComplexTypes.PublicProperties(type.ClrType))
            {
                if (property.PropertyType != typeof(Period) || NodaTimeComplexTypes.IsMappedByApplication(type, property, _memberClassifier))
                    continue;

                // Null when the property has been configured otherwise, at a higher level than a convention.
                var complexProperty = typeBuilder.ComplexProperty(property);
                if (complexProperty == null)
                    continue;

                var complexType = complexProperty.Metadata.ComplexType.Builder;
                foreach (var unit in Units)
                    complexType.Property(unit);
            }
        }

        /// <summary>
        /// Stores each Period of an entity type in a JSON column of its own, named after the property ("Every"), unless
        /// it's in a JSON column already. Done last, because only then is that known. EF Core can't put a JSON column
        /// inside a complex type stored in the table's own columns (dotnet/efcore#36558), so a Period there keeps a column
        /// for each unit: "Schedule_Every_Years" and so on.
        /// </summary>
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            NodaTimeComplexTypes.ThrowIfOnOwnedType(modelBuilder.Metadata, typeof(Period));
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
                MapToJson(entityType);
        }

        private static void MapToJson(IConventionTypeBase type)
        {
            foreach (var complexProperty in type.GetDeclaredComplexProperties())
            {
                var complexType = complexProperty.ComplexType;
                if (complexType.ClrType != typeof(Period))
                    MapToJson(complexType);
                else if (!complexType.IsMappedToJson() && type is IConventionEntityType && complexType.FindProperty(nameof(Period.Years)) is { } years)
                    complexType.SetContainerColumnName(NodaTimeComplexTypes.ColumnName(years, complexProperty));
            }
        }
    }
}
