using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using System.Data.Common;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    public class OffsetDateTimeTypeMapping : RelationalTypeMapping
    {
        private const string DateTimeOffsetFormatConst = "{0:yyyy-MM-ddTHH:mm:ss.fffffffzzz}";

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static OffsetDateTimeTypeMapping Default { get; } = new();

        public OffsetDateTimeTypeMapping()
            : base(CreateRelationalTypeMappingParameters())
        {
        }

        protected OffsetDateTimeTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new OffsetDateTimeTypeMapping(parameters);
        }

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters()
        {
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(OffsetDateTime),
                    new OffsetDateTimeValueConverter()),
                SqlServerDateTimeTypes.DateTimeOffset,
                // Without this, HasPrecision(n) is ignored and the column is always datetimeoffset(7).
                StoreTypePostfix.Precision);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected override void ConfigureParameter(DbParameter parameter)
        {
            base.ConfigureParameter(parameter);

            if (Size.HasValue
                && Size.Value != -1)
            {
                parameter.Size = Size.Value;
            }
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected override string SqlLiteralFormatString => $"'{DateTimeOffsetFormatConst}'";
    }
}
