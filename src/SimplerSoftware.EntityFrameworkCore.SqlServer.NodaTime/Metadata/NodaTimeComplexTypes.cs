using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata
{
    /// <summary>
    /// What the conventions that map NodaTime types as complex types share: <see cref="ZonedDateTimeConvention"/> (#22) and
    /// <see cref="PeriodConvention"/> (#3).
    /// </summary>
    internal static class NodaTimeComplexTypes
    {
        /// <summary>
        /// The type's public instance properties, including inherited ones (from a base class that isn't an entity type, the
        /// derived type has them, as it does its other properties). One per name, the most derived, for one hidden with "new".
        /// </summary>
        public static IEnumerable<PropertyInfo> PublicProperties(Type clrType)
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
        /// Whether the application maps the property itself, so a convention must leave it alone: one that can't be
        /// written, one ignored (by name, or with IgnoreAny&lt;T&gt;() in ConfigureConventions), or one EF Core maps as an
        /// ordinary property, as it does when ConfigureConventions gives its type a value converter
        /// (Properties&lt;T&gt;().HaveConversion(...)) or a type mapping.
        /// </summary>
        public static bool IsMappedByApplication(IConventionTypeBase type, PropertyInfo property, IMemberClassifier memberClassifier)
            => !property.CanRead || !property.CanWrite
                || type.IsIgnored(property.Name)
                || type.Model.IsIgnoredType(property.PropertyType)
                || memberClassifier.IsCandidatePrimitiveProperty(property, type.Model, useAttributes: true, out _, out _, out _);

        /// <summary>
        /// EF Core can't query a complex property of an owned entity type (it fails inside EF Core, whatever the table), and
        /// has no way to configure one, so a complex property of the given type there can only have come from these
        /// conventions. Rather than let every query of the owner fail, this says what to do instead.
        /// </summary>
        public static void ThrowIfOnOwnedType(IConventionModel model, Type clrType)
        {
            foreach (var entityType in model.GetEntityTypes().Where(e => e.IsOwned()))
            {
                if (FindComplexProperty(entityType, clrType) is { } complexProperty)
                {
                    throw new InvalidOperationException(
                        $"'{entityType.DisplayName()}.{complexProperty.Name}' is a {clrType.Name} on the owned entity type '{entityType.DisplayName()}'. "
                        + $"A {clrType.Name} is mapped as a complex type, which EF Core doesn't support on an owned entity type. "
                        + $"Make '{entityType.DisplayName()}' a complex type instead (ComplexProperty rather than OwnsOne), map the property "
                        + "yourself, for example with a value converter, or ignore it.");
                }
            }
        }

        private static IConventionComplexProperty FindComplexProperty(IConventionTypeBase type, Type clrType)
        {
            foreach (var complexProperty in type.GetDeclaredComplexProperties())
            {
                if (complexProperty.ComplexType.ClrType == clrType)
                    return complexProperty;
                if (FindComplexProperty(complexProperty.ComplexType, clrType) is { } nested)
                    return nested;
            }

            return null;
        }

        /// <summary>
        /// The name EF Core would give a column for the complex property itself: the default name of the column of one of
        /// its properties, without "_" and the property's name at the end. "Start", or "Schedule_Start" in a complex type of
        /// the application's own. So for a ZonedDateTime, it's the Zone column's name without "_Zone".
        /// </summary>
        public static string ColumnName(IConventionProperty property, IReadOnlyComplexProperty complexProperty)
        {
            var suffix = "_" + property.Name;
            var storeObject = StoreObjectIdentifier.Create(property.DeclaringType, StoreObjectType.Table)
                ?? StoreObjectIdentifier.Create(property.DeclaringType, StoreObjectType.View);
            var defaultName = storeObject is { } table ? property.GetDefaultColumnName(table) : property.GetDefaultColumnName();

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
}
