using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using NodaTime;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// A <see cref="Duration"/> stored in an int column as whole seconds: HasColumnType("int") (#38).
    /// Unlike time, it holds durations of 24 hours or more, and negative ones. Fractions of a second are truncated.
    /// </summary>
    public class DurationIntTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static DurationIntTypeMapping Default { get; } = new();

        public DurationIntTypeMapping()
            : base(CreateRelationalTypeMappingParameters())
        {
        }

        protected DurationIntTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new DurationIntTypeMapping(parameters);
        }

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters()
        {
            var converter = new DurationSecondsValueConverter();
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(Duration),
                    converter,
                    // Needed for collections of Duration, e.g. list.Contains(x.Duration) (#41). Writes JSON numbers.
                    jsonValueReaderWriter: new JsonConvertedValueReaderWriter<Duration, int>(JsonInt32ReaderWriter.Instance, converter)),
                "int",
                StoreTypePostfix.None,
                System.Data.DbType.Int32);
        }
    }
}
