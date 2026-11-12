using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using DateTimeTypeMapping = SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage.DateTimeTypeMapping;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    public class InstantTypeMapping : DateTimeTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static InstantTypeMapping Default { get; } = new(SqlServerDateTimeTypes.DateTime2);

        // EF Core needs a JSON reader/writer to handle collections of Instant, e.g. list.Contains(x.Instant) (#41).
        // Unlike the other types, Instant has its own so that numeric offsets in JSON are read correctly.
        public InstantTypeMapping(string storeType)
            : base(storeType, typeof(Instant), new InstantValueConverter(), JsonInstantReaderWriter.Instance)
        {
        }

        protected InstantTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new InstantTypeMapping(parameters);
        }
    }
}
