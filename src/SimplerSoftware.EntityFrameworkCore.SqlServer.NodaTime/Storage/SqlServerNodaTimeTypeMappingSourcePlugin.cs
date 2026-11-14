using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure;
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
        private readonly DurationBigIntTypeMapping _durationBigIntTypeMapping = new DurationBigIntTypeMapping();
        private readonly DurationIntTypeMapping _durationIntTypeMapping = new DurationIntTypeMapping();
        private readonly DurationDateTime2TypeMapping _durationDateTime2TypeMapping = new DurationDateTime2TypeMapping();
        private readonly DurationStringTypeMapping _durationNVarCharTypeMapping = new DurationStringTypeMapping("nvarchar", unicode: true, fixedLength: false);
        private readonly DurationStringTypeMapping _durationVarCharTypeMapping = new DurationStringTypeMapping("varchar", unicode: false, fixedLength: false);
        private readonly DurationStringTypeMapping _durationNCharTypeMapping = new DurationStringTypeMapping("nchar", unicode: true, fixedLength: true);
        private readonly DurationStringTypeMapping _durationCharTypeMapping = new DurationStringTypeMapping("char", unicode: false, fixedLength: true);

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

        private readonly OffsetTypeMapping _offsetTypeMapping = new OffsetTypeMapping();
        private readonly IDateTimeZoneProvider _dateTimeZoneProvider;

        /// <summary>
        /// Constructs an instance of the <see cref="SqlServerNodaTimeTypeMappingSourcePlugin"/> class, which reads
        /// <see cref="DateTimeZone"/> values with the tzdb provider.
        /// </summary>
        public SqlServerNodaTimeTypeMappingSourcePlugin()
            : this(DateTimeZoneProviders.Tzdb)
        {
        }

        /// <summary>
        /// Constructs an instance of the <see cref="SqlServerNodaTimeTypeMappingSourcePlugin"/> class, which reads
        /// <see cref="DateTimeZone"/> values with the configured provider (UseNodaTime(n =&gt; n.UseDateTimeZoneProvider(...))).
        /// </summary>
        public SqlServerNodaTimeTypeMappingSourcePlugin(INodaTimeSingletonOptions options)
            : this(options.DateTimeZoneProvider)
        {
        }

        private SqlServerNodaTimeTypeMappingSourcePlugin(IDateTimeZoneProvider dateTimeZoneProvider)
        {
            _dateTimeZoneProvider = dateTimeZoneProvider;

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
            // Only used when the column type is set to bigint, int, datetime2 or text (#38); time, above, stays the default for Duration.
            this.AddTypeMappingToDictionaries(this._durationBigIntTypeMapping);
            this.AddTypeMappingToDictionaries(this._durationIntTypeMapping);
            this.AddTypeMappingToDictionaries(this._durationDateTime2TypeMapping);
            this.AddTypeMappingToDictionaries(this._durationNVarCharTypeMapping);
            this.AddTypeMappingToDictionaries(this._durationVarCharTypeMapping);
            this.AddTypeMappingToDictionaries(this._durationNCharTypeMapping);
            this.AddTypeMappingToDictionaries(this._durationCharTypeMapping);

            // A DateTimeZone as its ID (#22): nvarchar(50) by default, or another string type set by the column type.
            // Only by CLR type: registering "nvarchar(50)" as a store type would hide it from a Duration stored as text.
            this.ClrTypeMappings.TryAdd(typeof(DateTimeZone), new DateTimeZoneTypeMapping(dateTimeZoneProvider));
            foreach (var unicode in new[] { true, false })
            {
                foreach (var fixedLength in new[] { false, true })
                {
                    var storeType = StringStoreType(unicode, fixedLength);
                    this.StoreTypeMappings[storeType].Add(new DateTimeZoneTypeMapping(dateTimeZoneProvider, storeType, unicode, fixedLength, size: null));
                }
            }

            // An Offset as int seconds (#22).
            this.AddTypeMappingToDictionaries(this._offsetTypeMapping);
            // SQL Server's synonym for int, which EF Core also accepts.
            this.StoreTypeMappings["integer"] = new List<RelationalTypeMapping> { this._durationIntTypeMapping, this._offsetTypeMapping };
        }

        public RelationalTypeMapping FindMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;

            // A zone's runtime type is a subclass, such as the one for tzdb zones; a constant in a query has that type.
            if (clrType != null && clrType != typeof(DateTimeZone) && typeof(DateTimeZone).IsAssignableFrom(clrType))
                clrType = typeof(DateTimeZone);
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
                    // expressions like DateTime.UtcNow with InstantValueConverter (#52).
                    // Scaffolding gets NodaTime types from SqlServerNodaTimeScaffoldingTypeMappingSourcePlugin.
                    if (clrType == null)
                        return null;

                    foreach (var m in mappings)
                    {
                        if (m.ClrType != clrType)
                            continue;

                        if (m is DurationStringTypeMapping or DateTimeZoneTypeMapping)
                            return TextMapping(m, mappingInfo, storeTypeNameBase);

                        // HasColumnType("datetime2").HasPrecision(3) means datetime2(3), as it does for EF Core's own types.
                        // Types that take no precision (datetime, smalldatetime, date) ignore it.
                        return mappingInfo.Precision.HasValue && m.StoreTypePostfix == StoreTypePostfix.Precision ? m.Clone(mappingInfo) : m;
                    }

                    return null;
                }

                if (StoreTypeMappings.TryGetValue(storeTypeNameBase, out mappings))
                {
                    if (clrType == null)
                        return null;

                    foreach (var m in mappings)
                        if (m.ClrType == clrType)
                            return m is DurationStringTypeMapping or DateTimeZoneTypeMapping ? TextMapping(m, mappingInfo, storeTypeNameBase) : m.Clone(mappingInfo);

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

            // HasMaxLength, IsUnicode(false) or IsFixedLength() without a column type: varchar(32), say.
            if (mapping is DateTimeZoneTypeMapping && (mappingInfo.Size is > 0 || mappingInfo.IsUnicode == false || mappingInfo.IsFixedLength == true))
            {
                var unicode = mappingInfo.IsUnicode ?? true;
                var fixedLength = mappingInfo.IsFixedLength ?? false;
                return new DateTimeZoneTypeMapping(
                    _dateTimeZoneProvider, StringStoreType(unicode, fixedLength), unicode, fixedLength, mappingInfo.Size is > 0 ? mappingInfo.Size : DateTimeZoneTypeMapping.DefaultLength);
            }

            // The default type with the precision: HasPrecision(3) alone gives datetime2(3). With an alias type name, this
            // keeps the default type too: "dbo.UtcTimestamp(3)" wouldn't be valid SQL where the name is used, as in OPENJSON.
            return mappingInfo.Precision.HasValue && mapping.StoreTypePostfix == StoreTypePostfix.Precision
                ? mapping.WithPrecisionAndScale(mappingInfo.Precision, mappingInfo.Scale)
                : mapping;
        }

        /// <summary>
        /// A Duration stored as text needs room for the longest value, <see cref="DurationStringValueConverter.MaxLength"/>
        /// characters. A shorter length is refused like any other unsupported column type, since saving would fail later
        /// (perhaps only for a negative value). No length at all, as in plain "nvarchar" (which SQL Server reads as
        /// nvarchar(1)), means 28, or 50 for a DateTimeZone's ID; one from HasMaxLength is kept.
        /// </summary>
        private static RelationalTypeMapping TextMapping(RelationalTypeMapping mapping, in RelationalTypeMappingInfo mappingInfo, string storeTypeNameBase)
        {
            var isDuration = mapping is DurationStringTypeMapping;
            if (isDuration && mappingInfo.Size is > 0 and < DurationStringValueConverter.MaxLength)
                return null;

            if (mappingInfo.Size == null && !mappingInfo.StoreTypeName.Contains('('))
                return mapping.WithStoreTypeAndSize(storeTypeNameBase, isDuration ? DurationStringValueConverter.MaxLength : DateTimeZoneTypeMapping.DefaultLength);

            return mapping.Clone(mappingInfo);
        }

        private static string StringStoreType(bool unicode, bool fixedLength)
            => (unicode ? "n" : "") + (fixedLength ? "char" : "varchar");

        private void AddTypeMappingToDictionaries(RelationalTypeMapping relationalTypeMapping)
        {
            this.ClrTypeMappings.TryAdd(relationalTypeMapping.ClrType, relationalTypeMapping);
            this.StoreTypeMappings.TryAdd(relationalTypeMapping.StoreType, new List<RelationalTypeMapping>());
            this.StoreTypeMappings[relationalTypeMapping.StoreType].Add(relationalTypeMapping);
        }
    }
}
