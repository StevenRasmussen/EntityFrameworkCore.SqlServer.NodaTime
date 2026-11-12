using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage
{
    public class SqlServerNodaTimeTypeMappingSourcePlugin : IRelationalTypeMappingSourcePlugin
    {
        // SQL Server type names aren't case-sensitive, so HasColumnType("DATETIME2") must find the "datetime2" mappings.
        public virtual ConcurrentDictionary<string, List<RelationalTypeMapping>> StoreTypeMappings { get; } = new ConcurrentDictionary<string, List<RelationalTypeMapping>>(StringComparer.OrdinalIgnoreCase);
        public virtual ConcurrentDictionary<Type, RelationalTypeMapping> ClrTypeMappings { get; } = new ConcurrentDictionary<Type, RelationalTypeMapping>();

        private readonly InstantTypeMapping _instantSmallDateTimeTypeMapping = new InstantTypeMapping(SqlServerDateTimeTypes.SmallDateTime);
        private readonly InstantTypeMapping _instantDateTimeTypeMapping = new InstantTypeMapping(SqlServerDateTimeTypes.DateTime);
        private readonly InstantTypeMapping _instantDateTime2TypeMapping = new InstantTypeMapping(SqlServerDateTimeTypes.DateTime2);
        private readonly OffsetDateTimeTypeMapping _offsetDateTimeTypeMapping = new OffsetDateTimeTypeMapping();
        private readonly LocalDateTimeTypeMapping _localDateTimeSmallDateTimeTypeMapping = new LocalDateTimeTypeMapping(SqlServerDateTimeTypes.SmallDateTime);
        private readonly LocalDateTimeTypeMapping _localDateTimeDateTimeTypeMapping = new LocalDateTimeTypeMapping(SqlServerDateTimeTypes.DateTime);
        private readonly LocalDateTimeTypeMapping _localDateTimeDateTime2TypeMapping = new LocalDateTimeTypeMapping(SqlServerDateTimeTypes.DateTime2);
        private readonly LocalDateTypeMapping _localDateTypeMapping = new LocalDateTypeMapping();
        private readonly LocalTimeTypeMapping _localTimeTypeMapping = new LocalTimeTypeMapping();
        private readonly DurationTypeMapping _durationTypeMapping = new DurationTypeMapping();

        private readonly static List<Type> _hasPrecisionTypes = new List<Type>
        {
            typeof(Instant),
            typeof(OffsetDateTime),
            typeof(LocalDateTime),
            typeof(LocalTime),
            typeof(Duration),
        };

        // SQL Server's built-in type names (and their ISO synonyms). A NodaTime property configured with one of these
        // that we don't map it to (e.g. bigint on a Duration) is a misconfiguration. Any other name may be a
        // user-defined alias type (CREATE TYPE ... FROM date), which keeps the default mapping as it always has.
        private static readonly HashSet<string> _sqlServerBuiltInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bigint", "binary", "bit", "char", "date", "datetime", "datetime2", "datetimeoffset", "decimal", "float",
            "geography", "geometry", "hierarchyid", "image", "int", "json", "money", "nchar", "ntext", "numeric",
            "nvarchar", "real", "rowversion", "smalldatetime", "smallint", "smallmoney", "sql_variant", "sysname",
            "text", "time", "timestamp", "tinyint", "uniqueidentifier", "varbinary", "varchar", "vector", "xml",
            "binary varying", "char varying", "character", "character varying", "dec", "double precision", "integer",
            "national char", "national char varying", "national character", "national character varying", "national text",
        };

        /// <summary>
        /// Constructs an instance of the <see cref="SqlServerNodaTimeTypeMappingSourcePlugin"/> class.
        /// </summary>
        public SqlServerNodaTimeTypeMappingSourcePlugin()
        {
            // The order these are added matters.
            // In the case of multiple db types mapping to the same CLR type, 
            // the first CLR type will be the default mapping type when going from CLRType -> DBType
            this.AddTypeMappingToDictionaries(this._instantDateTime2TypeMapping);
            this.AddTypeMappingToDictionaries(this._instantDateTimeTypeMapping);
            this.AddTypeMappingToDictionaries(this._instantSmallDateTimeTypeMapping);
            this.AddTypeMappingToDictionaries(this._offsetDateTimeTypeMapping);
            this.AddTypeMappingToDictionaries(this._localDateTimeDateTime2TypeMapping);
            this.AddTypeMappingToDictionaries(this._localDateTimeDateTimeTypeMapping);
            this.AddTypeMappingToDictionaries(this._localDateTimeSmallDateTimeTypeMapping);
            this.AddTypeMappingToDictionaries(this._localDateTypeMapping);
            this.AddTypeMappingToDictionaries(this._localTimeTypeMapping);
            this.AddTypeMappingToDictionaries(this._durationTypeMapping);
        }

        public RelationalTypeMapping FindMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;
            var storeTypeName = mappingInfo.StoreTypeName;
            // "datetime2 (3)" can leave a trailing space on the base name.
            var storeTypeNameBase = mappingInfo.StoreTypeNameBase?.Trim() ?? storeTypeName;

            if (storeTypeName != null)
            {
                if (StoreTypeMappings.TryGetValue(storeTypeName, out var mappings))
                {
                    // When clrType is null, defer to EF Core's default mapping source: our store
                    // types (datetime, datetime2, etc.) all have non-NodaTime defaults that EF
                    // can supply. Returning a NodaTime mapping here pollutes plain DateTime
                    // expressions like DateTime.UtcNow with InstantValueConverter.
                    if (clrType == null)
                        return null;

                    foreach (var m in mappings)
                        if (m.ClrType == clrType)
                            // HasColumnType("datetime2").HasPrecision(3) means datetime2(3), as it does for EF Core's own types.
                            // Types that take no precision (datetime, smalldatetime, date) ignore it.
                            return mappingInfo.Precision.HasValue && m.StoreTypePostfix == StoreTypePostfix.Precision ? m.Clone(mappingInfo) : m;

                    return null;
                }

                if (StoreTypeMappings.TryGetValue(storeTypeNameBase, out mappings))
                {
                    if (clrType == null)
                        return null;

                    foreach (var m in mappings)
                        if (m.ClrType == clrType)
                            return m.Clone(mappingInfo);

                    return null;
                }

                // A built-in column type we don't map NodaTime types to (e.g. HasColumnType("bigint") on a Duration).
                // Returning null makes EF Core report that the property can't be mapped to that type; falling through
                // to the default mapping below would silently ignore the configured column type.
                if (_sqlServerBuiltInTypes.Contains(storeTypeNameBase))
                    return null;
            }

            if (clrType == null || !ClrTypeMappings.TryGetValue(clrType, out var mapping))
                return null;

            // TODO: Cache size/precision/scale mappings?
            return mappingInfo.Precision.HasValue && _hasPrecisionTypes.Contains(mapping.ClrType)
                ? mapping.Clone(mappingInfo)
                : mapping;
        }

        private void AddTypeMappingToDictionaries(RelationalTypeMapping relationalTypeMapping)
        {
            this.ClrTypeMappings.TryAdd(relationalTypeMapping.ClrType, relationalTypeMapping);
            this.StoreTypeMappings.TryAdd(relationalTypeMapping.StoreType, new List<RelationalTypeMapping>());
            this.StoreTypeMappings[relationalTypeMapping.StoreType].Add(relationalTypeMapping);
        }
    }
}
