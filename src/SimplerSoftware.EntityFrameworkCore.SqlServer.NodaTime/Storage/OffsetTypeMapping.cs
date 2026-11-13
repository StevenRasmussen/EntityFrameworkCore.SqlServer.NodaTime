using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>Stores an <see cref="Offset"/> as its whole seconds, NodaTime's own precision for an offset (#22).</summary>
    public class OffsetValueConverter : ValueConverter<Offset, int>
    {
        public OffsetValueConverter()
            : base(o => toProvider(o), s => fromProvider(s))
        {
        }

        public static int toProvider(Offset offset) => offset.Seconds;

        public static Offset fromProvider(int seconds) => Offset.FromSeconds(seconds);
    }

    /// <summary>An <see cref="Offset"/> stored as int seconds, e.g. 19800 for +05:30 (#22).</summary>
    public class OffsetTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static OffsetTypeMapping Default { get; } = new();

        public OffsetTypeMapping()
            : base(new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(Offset),
                    new OffsetValueConverter(),
                    // Needed for collections, e.g. offsets.Contains(x.Offset) (#41).
                    jsonValueReaderWriter: new JsonConvertedValueReaderWriter<Offset, int>(JsonInt32ReaderWriter.Instance, new OffsetValueConverter())),
                "int",
                StoreTypePostfix.None,
                System.Data.DbType.Int32))
        {
        }

        protected OffsetTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new OffsetTypeMapping(parameters);
        }
    }
}
