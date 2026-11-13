using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using NodaTime;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// A <see cref="Duration"/> stored in a bigint column as nanoseconds: HasColumnType("bigint") (#38).
    /// Unlike time, it holds durations of 24 hours or more, and negative ones.
    /// </summary>
    public class DurationBigIntTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static DurationBigIntTypeMapping Default { get; } = new();

        public DurationBigIntTypeMapping()
            : base(CreateRelationalTypeMappingParameters())
        {
        }

        protected DurationBigIntTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new DurationBigIntTypeMapping(parameters);
        }

        /// <summary>
        /// SQL Server types a literal outside the int range, such as 90000000000000, as numeric rather than bigint,
        /// and dividing by a numeric isn't integer division. EF Core writes its own long literals the same way.
        /// </summary>
        protected override string SqlLiteralFormatString => "CAST({0} AS bigint)";

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters()
        {
            var converter = new DurationNanosecondsValueConverter();
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(Duration),
                    converter,
                    // Needed for collections of Duration, e.g. list.Contains(x.Duration) (#41). Writes JSON numbers.
                    jsonValueReaderWriter: new JsonConvertedValueReaderWriter<Duration, long>(JsonInt64ReaderWriter.Instance, converter)),
                "bigint",
                StoreTypePostfix.None,
                System.Data.DbType.Int64);
        }
    }
}
