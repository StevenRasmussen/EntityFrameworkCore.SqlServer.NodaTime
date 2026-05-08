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
        public virtual ConcurrentDictionary<string, List<RelationalTypeMapping>> StoreTypeMappings { get; } = new ConcurrentDictionary<string, List<RelationalTypeMapping>>();
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
            var storeTypeNameBase = mappingInfo.StoreTypeNameBase;

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
                            return m;

                    return null;
                }

                if (StoreTypeMappings.TryGetValue(storeTypeNameBase!, out mappings))
                {
                    if (clrType == null)
                        return null;

                    foreach (var m in mappings)
                        if (m.ClrType == clrType)
                            return m.Clone(mappingInfo);

                    return null;
                }
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
