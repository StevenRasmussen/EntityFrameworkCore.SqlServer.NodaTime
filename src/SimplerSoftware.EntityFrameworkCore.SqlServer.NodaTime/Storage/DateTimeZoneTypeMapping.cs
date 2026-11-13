using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;
using System.Data.Common;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// Stores a <see cref="DateTimeZone"/> as its ID, such as "Europe/Paris" (#22). Reading it back looks the ID up in a
    /// time zone provider: tzdb unless another is chosen with UseNodaTime(n =&gt; n.UseDateTimeZoneProvider(...)).
    /// </summary>
    public class DateTimeZoneValueConverter : ValueConverter<DateTimeZone, string>
    {
        /// <summary>Uses <see cref="DateTimeZoneProviders.Tzdb"/>.</summary>
        public DateTimeZoneValueConverter()
            : this(DateTimeZoneProviders.Tzdb)
        {
        }

        public DateTimeZoneValueConverter(IDateTimeZoneProvider provider)
            : base(zone => toProvider(zone), FromProviderExpression(provider))
        {
            Provider = provider;
        }

        public IDateTimeZoneProvider Provider { get; }

        public static string toProvider(DateTimeZone zone) => zone.Id;

        // Each trims the spaces a char or nchar column pads the ID with; no zone ID ends in one.

        /// <exception cref="DateTimeZoneNotFoundException">The tzdb provider has no zone with this ID.</exception>
        public static DateTimeZone fromTzdb(string id) => DateTimeZoneProviders.Tzdb[id.TrimEnd(' ')];

        /// <exception cref="DateTimeZoneNotFoundException">The BCL provider has no zone with this ID.</exception>
        public static DateTimeZone fromBcl(string id) => DateTimeZoneProviders.Bcl[id.TrimEnd(' ')];

        // NodaTime's own providers through static methods, which a compiled model's generated code can call; any
        // other provider is captured, which works except in a compiled model.
        private static Expression<Func<string, DateTimeZone>> FromProviderExpression(IDateTimeZoneProvider provider)
        {
            if (ReferenceEquals(provider, DateTimeZoneProviders.Tzdb))
                return id => fromTzdb(id);
            if (ReferenceEquals(provider, DateTimeZoneProviders.Bcl))
                return id => fromBcl(id);
            return id => provider[id.TrimEnd(' ')];
        }
    }

    /// <summary>
    /// A <see cref="DateTimeZone"/> stored as its ID (#22): nvarchar(50) by default, or the length or string type set with
    /// HasColumnType or HasMaxLength. tzdb IDs are at most 32 characters, Windows ones 31.
    /// </summary>
    public class DateTimeZoneTypeMapping : RelationalTypeMapping
    {
        public const int DefaultLength = 50;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static DateTimeZoneTypeMapping Default { get; } = new(DateTimeZoneProviders.Tzdb);

        public DateTimeZoneTypeMapping(IDateTimeZoneProvider provider)
            : this(provider, "nvarchar", unicode: true, fixedLength: false, DefaultLength)
        {
        }

        /// <param name="storeType">nvarchar, varchar, nchar or char, without a length.</param>
        public DateTimeZoneTypeMapping(IDateTimeZoneProvider provider, string storeType, bool unicode, bool fixedLength, int? size)
            : base(CreateRelationalTypeMappingParameters(provider, storeType, unicode, fixedLength, size))
        {
        }

        protected DateTimeZoneTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new DateTimeZoneTypeMapping(parameters);
        }

        protected override string GenerateNonNullSqlLiteral(object value)
            => (IsUnicode ? "N'" : "'") + ((string)value).Replace("'", "''") + "'";

        /// <summary>
        /// As EF Core does for strings: a value is sent with the column's length (or the default), so SQL Server can reuse
        /// one plan, unless it is longer. Then it is sent as (n)varchar(max), so that SQL Server refuses to save it rather
        /// than SqlClient cutting it short.
        /// </summary>
        protected override void ConfigureParameter(DbParameter parameter)
        {
            base.ConfigureParameter(parameter);

            var size = Size ?? DefaultLength;
            parameter.Size = size > 0 && !(parameter.Value is string id && id.Length > size) ? size : -1;
        }

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters(
            IDateTimeZoneProvider provider, string storeType, bool unicode, bool fixedLength, int? size)
        {
            var converter = new DateTimeZoneValueConverter(provider);
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(DateTimeZone),
                    converter,
                    // Needed for collections, e.g. zones.Contains(x.Zone) (#41).
                    jsonValueReaderWriter: new JsonConvertedValueReaderWriter<DateTimeZone, string>(JsonStringReaderWriter.Instance, converter)),
                storeType,
                StoreTypePostfix.Size,
                unicode ? (fixedLength ? System.Data.DbType.StringFixedLength : System.Data.DbType.String) : (fixedLength ? System.Data.DbType.AnsiStringFixedLength : System.Data.DbType.AnsiString),
                unicode: unicode,
                size: size,
                fixedLength: fixedLength);
        }
    }
}
