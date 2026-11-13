using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
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
            foreach (var property in PublicProperties(type.ClrType))
            {
                // Not one the application maps itself: one ignored (by name, or with IgnoreAny<ZonedDateTime?>()), or one
                // EF Core maps as an ordinary property, as it does when ConfigureConventions gives ZonedDateTime a value
                // converter (Properties<ZonedDateTime>().HaveConversion(...)) or a type mapping.
                if ((property.PropertyType != typeof(ZonedDateTime) && property.PropertyType != typeof(ZonedDateTime?))
                    || !property.CanRead || !property.CanWrite
                    || type.IsIgnored(property.Name)
                    || type.Model.IsIgnoredType(property.PropertyType)
                    || _memberClassifier.IsCandidatePrimitiveProperty(property, type.Model, useAttributes: true, out _, out _, out _))
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
        /// The type's public instance properties, including inherited ones (from a base class that isn't an entity type, the
        /// derived type has them, as it does its other properties). One per name, the most derived, for one hidden with "new".
        /// </summary>
        private static IEnumerable<PropertyInfo> PublicProperties(Type clrType)
        {
            var names = new HashSet<string>();
            for (var current = clrType; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    // Not an indexer, which EF Core doesn't map as a property either.
                    if (property.GetIndexParameters().Length == 0 && names.Add(property.Name))
                        yield return property;
                }
            }
        }

        /// <summary>
        /// Names the OffsetDateTime's column after the ZonedDateTime property ("Start" rather than "Start_offsetDateTime"),
        /// or in a JSON column, its JSON property "OffsetDateTime", beside "Zone". Done last, because only then is it known
        /// whether the property is in a JSON column.
        /// </summary>
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
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
                    offsetDateTime.Builder.HasColumnName(ColumnName(offsetDateTime, complexProperty));
            }
        }

        /// <summary>
        /// The name EF Core would give the column, without the "_offsetDateTime" at the end: "Start", "Schedule_Start", or
        /// for an owned type in its owner's table, "Booking_Start". So it's the Zone column's name without "_Zone".
        /// </summary>
        private static string ColumnName(IConventionProperty offsetDateTime, IReadOnlyComplexProperty complexProperty)
        {
            const string suffix = "_" + OffsetDateTimePropertyName;
            var storeObject = StoreObjectIdentifier.Create(offsetDateTime.DeclaringType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(offsetDateTime.DeclaringType, StoreObjectType.View);
            var defaultName = storeObject is { } table ? offsetDateTime.GetDefaultColumnName(table) : offsetDateTime.GetDefaultColumnName();

            // Not when EF Core has shortened a long name to fit.
            return defaultName != null && defaultName.EndsWith(suffix, StringComparison.Ordinal)
                ? defaultName[..^suffix.Length]
                : ColumnPrefix(complexProperty);
        }

        /// <summary>The column-name prefix EF Core uses for a complex property's columns: "Start", or "Schedule_Start".</summary>
        private static string ColumnPrefix(IReadOnlyComplexProperty complexProperty)
        {
            var names = new List<string>();
            for (var current = complexProperty; current != null; current = (current.DeclaringType as IReadOnlyComplexType)?.ComplexProperty)
                names.Insert(0, current.Name);
            return string.Join("_", names);
        }
    }

    internal sealed class NodaTimeConventionSetPlugin : IConventionSetPlugin
    {
        private readonly ProviderConventionSetBuilderDependencies _dependencies;

        public NodaTimeConventionSetPlugin(ProviderConventionSetBuilderDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            var convention = new ZonedDateTimeConvention(_dependencies.MemberClassifier);
            conventionSet.EntityTypeAddedConventions.Add(convention);
            conventionSet.ComplexPropertyAddedConventions.Add(convention);
            // First, so that EF Core's own finalizing conventions (such as making column names unique) see the names.
            conventionSet.ModelFinalizingConventions.Insert(0, convention);
            return conventionSet;
        }
    }
}
