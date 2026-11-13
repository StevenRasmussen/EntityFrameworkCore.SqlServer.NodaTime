using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using NodaTime;
using System.Data.Common;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// A <see cref="Duration"/> stored as text: HasColumnType("nvarchar(28)"), or varchar, nchar or char of at least
    /// <see cref="DurationStringValueConverter.MaxLength"/> characters (#38). Only equality is translated in queries.
    /// </summary>
    public class DurationStringTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static DurationStringTypeMapping Default { get; } = new("nvarchar", unicode: true, fixedLength: false);

        /// <param name="storeType">nvarchar, varchar, nchar or char, without a length.</param>
        public DurationStringTypeMapping(string storeType, bool unicode, bool fixedLength)
            : base(CreateRelationalTypeMappingParameters(storeType, unicode, fixedLength))
        {
        }

        protected DurationStringTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new DurationStringTypeMapping(parameters);
        }

        /// <summary>The text never contains a quote, so it needs no escaping.</summary>
        protected override string SqlLiteralFormatString => IsUnicode ? "N'{0}'" : "'{0}'";

        /// <summary>
        /// The parameter gets the column's length, or 28 for nvarchar(max), so that every value is sent with the same
        /// size and SQL Server can reuse one query plan.
        /// </summary>
        protected override void ConfigureParameter(DbParameter parameter)
        {
            base.ConfigureParameter(parameter);

            parameter.Size = Size is > 0 ? Size.Value : DurationStringValueConverter.MaxLength;
        }

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters(string storeType, bool unicode, bool fixedLength)
        {
            var converter = new DurationStringValueConverter();
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(Duration),
                    converter,
                    // Needed for collections of Duration, e.g. list.Contains(x.Duration) (#41).
                    jsonValueReaderWriter: new JsonConvertedValueReaderWriter<Duration, string>(JsonStringReaderWriter.Instance, converter)),
                storeType,
                StoreTypePostfix.Size,
                unicode ? (fixedLength ? System.Data.DbType.StringFixedLength : System.Data.DbType.String) : (fixedLength ? System.Data.DbType.AnsiStringFixedLength : System.Data.DbType.AnsiString),
                unicode: unicode,
                fixedLength: fixedLength);
        }
    }
}
