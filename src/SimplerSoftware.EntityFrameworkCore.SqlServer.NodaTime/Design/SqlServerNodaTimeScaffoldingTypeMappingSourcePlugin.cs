using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using System;
using System.Collections.Generic;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design
{
    /// <summary>
    /// Gives date and time columns NodaTime types when scaffolding ('dotnet ef dbcontext scaffold'), which looks each
    /// column's type up by its store type alone. <see cref="SqlServerNodaTimeTypeMappingSourcePlugin"/> leaves those
    /// lookups to EF Core, because queries make them too, as for DateTime.UtcNow, and need EF Core's types (#52).
    /// This is only registered with the design-time services. Where there's a DbContext, as for migrations, EF Core
    /// uses the context's own type mapping source instead, so this only affects scaffolding.
    /// </summary>
    internal sealed class SqlServerNodaTimeScaffoldingTypeMappingSourcePlugin : IRelationalTypeMappingSourcePlugin
    {
        // Only the date and time types. A Duration, Offset or DateTimeZone can be stored as a number or text, but most
        // int, bigint and nvarchar columns aren't, so they keep EF Core's types.
        private readonly Dictionary<string, RelationalTypeMapping> _mappings = new(StringComparer.OrdinalIgnoreCase)
        {
            [SqlServerDateTimeTypes.DateTime2] = new InstantTypeMapping(SqlServerDateTimeTypes.DateTime2),
            [SqlServerDateTimeTypes.DateTime] = new InstantTypeMapping(SqlServerDateTimeTypes.DateTime),
            [SqlServerDateTimeTypes.SmallDateTime] = new InstantTypeMapping(SqlServerDateTimeTypes.SmallDateTime),
            [SqlServerDateTimeTypes.DateTimeOffset] = new OffsetDateTimeTypeMapping(),
            [SqlServerDateTimeTypes.Date] = new LocalDateTypeMapping(),
            [SqlServerDateTimeTypes.Time] = new LocalTimeTypeMapping(),
        };

        public RelationalTypeMapping FindMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            if (mappingInfo.ClrType != null || mappingInfo.StoreTypeName == null)
                return null;

            if (_mappings.TryGetValue(mappingInfo.StoreTypeName, out var mapping))
                return mapping;

            // datetime2(3): the mapping with that precision. "datetime2 (3)" can leave a trailing space on the base name.
            var storeTypeNameBase = mappingInfo.StoreTypeNameBase?.Trim();
            return storeTypeNameBase != null && _mappings.TryGetValue(storeTypeNameBase, out mapping)
                ? mapping.Clone(mappingInfo)
                : null;
        }
    }
}
