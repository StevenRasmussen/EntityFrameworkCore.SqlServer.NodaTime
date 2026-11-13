using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using System;
using DateTimeTypeMapping = SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage.DateTimeTypeMapping;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// A <see cref="Duration"/> stored in a datetime2 column as an offset from 1900-01-01: HasColumnType("datetime2") (#38).
    /// Unlike time, it holds durations of 24 hours or more, and negative ones.
    /// </summary>
    public class DurationDateTime2TypeMapping : DateTimeTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static DurationDateTime2TypeMapping Default { get; } = new();

        public DurationDateTime2TypeMapping()
            : base(SqlServerDateTimeTypes.DateTime2, typeof(Duration), new DurationDateTime2ValueConverter(), CreateJsonValueReaderWriter())
        {
        }

        // Needed for collections of Duration, e.g. list.Contains(x.Duration) (#41): the same JSON as a DateTime.
        private static JsonValueReaderWriter CreateJsonValueReaderWriter()
            => new JsonConvertedValueReaderWriter<Duration, DateTime>(JsonDateTimeReaderWriter.Instance, new DurationDateTime2ValueConverter());

        protected DurationDateTime2TypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new DurationDateTime2TypeMapping(parameters);
        }
    }
}
