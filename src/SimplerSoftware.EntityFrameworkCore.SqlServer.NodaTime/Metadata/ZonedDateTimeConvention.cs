using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using NodaTime;
using System;
using System.Reflection;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata
{
    /// <summary>
    /// Maps every <see cref="ZonedDateTime"/> property, on an entity type or a complex type, as a complex type over the two
    /// fields a ZonedDateTime is made of (#22): its OffsetDateTime, in a datetimeoffset column named after the property
    /// ("Start"), and its zone, in a column for the zone ID ("Start_Zone"). The application's own mapping takes precedence:
    /// a property ignored, or given a value converter, whether by itself or for every ZonedDateTime in ConfigureConventions.
    /// </summary>
    internal sealed class ZonedDateTimeConvention : IEntityTypeAddedConvention, IComplexPropertyAddedConvention, IModelFinalizingConvention
    {
        private readonly IMemberClassifier _memberClassifier;

        public ZonedDateTimeConvention(IMemberClassifier memberClassifier)
        {
            _memberClassifier = memberClassifier;
        }

        /// <summary>The name of the property mapped to a ZonedDateTime's OffsetDateTime; queries use it with EF.Property.</summary>
        internal const string OffsetDateTimePropertyName = "offsetDateTime";

        private static readonly FieldInfo _offsetDateTimeField
            = typeof(ZonedDateTime).GetField(OffsetDateTimePropertyName, BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo _zoneField
            = typeof(ZonedDateTime).GetField("zone", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly PropertyInfo _zoneProperty = typeof(ZonedDateTime).GetProperty(nameof(ZonedDateTime.Zone));

        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
            => AddZonedDateTimeProperties(entityTypeBuilder);

        public void ProcessComplexPropertyAdded(IConventionComplexPropertyBuilder propertyBuilder, IConventionContext<IConventionComplexPropertyBuilder> context)
        {
            var complexType = propertyBuilder.Metadata.ComplexType;
            if (complexType.ClrType != typeof(ZonedDateTime))
                AddZonedDateTimeProperties(complexType.Builder);
        }

        private void AddZonedDateTimeProperties(IConventionTypeBaseBuilder typeBuilder)
        {
            var type = typeBuilder.Metadata;

            // IgnoreAny<ZonedDateTime>() in ConfigureConventions.
            if (type.Model.IsIgnoredType(typeof(ZonedDateTime)))
                return;

            foreach (var property in NodaTimeComplexTypes.PublicProperties(type.ClrType))
            {
                if ((property.PropertyType != typeof(ZonedDateTime) && property.PropertyType != typeof(ZonedDateTime?))
                    || NodaTimeComplexTypes.IsMappedByApplication(type, property, _memberClassifier))
                {
                    continue;
                }

                if (_offsetDateTimeField == null || _zoneField == null)
                {
                    throw new InvalidOperationException(
                        $"'{type.DisplayName()}.{property.Name}' is a ZonedDateTime, which is mapped through its private fields "
                        + "'offsetDateTime' and 'zone'. This version of NodaTime doesn't have them; map the property yourself, "
                        + "for example with a value converter, or ignore it.");
                }

                // Null when the property has been configured otherwise, at a higher level than a convention.
                var complexProperty = typeBuilder.ComplexProperty(property);
                if (complexProperty == null)
                    continue;

                var complexType = complexProperty.Metadata.ComplexType.Builder;
                complexType.Property(_offsetDateTimeField);

                // The public Zone property, set through its field when a value is read: EF Core names the column Start_Zone
                // by itself. It is read through the property, which gives UTC for a default ZonedDateTime, whose field is null.
                complexType.Property(_zoneProperty)?.HasField(_zoneField)?.UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
            }
        }

        /// <summary>
        /// Names the OffsetDateTime's column after the ZonedDateTime property ("Start" rather than "Start_offsetDateTime"),
        /// or in a JSON column, its JSON property "OffsetDateTime", beside "Zone". Done last, because only then is it known
        /// whether the property is in a JSON column.
        /// </summary>
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            NodaTimeComplexTypes.ThrowIfOnOwnedType(modelBuilder.Metadata, typeof(ZonedDateTime));
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
                NameColumns(entityType);
        }

        private static void NameColumns(IConventionTypeBase type)
        {
            foreach (var complexProperty in type.GetDeclaredComplexProperties())
            {
                var complexType = complexProperty.ComplexType;
                if (complexType.ClrType != typeof(ZonedDateTime))
                    NameColumns(complexType);
                else if (complexType.IsMappedToJson())
                    complexType.FindProperty(OffsetDateTimePropertyName)?.Builder.HasJsonPropertyName(nameof(OffsetDateTime));
                else if (complexType.FindProperty(OffsetDateTimePropertyName) is { } offsetDateTime)
                    offsetDateTime.Builder.HasColumnName(NodaTimeComplexTypes.ColumnName(offsetDateTime, complexProperty));
            }
        }
    }
}
