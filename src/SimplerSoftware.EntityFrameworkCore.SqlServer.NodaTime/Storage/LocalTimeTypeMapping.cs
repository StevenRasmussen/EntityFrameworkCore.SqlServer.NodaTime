using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;
using System.Data;
using System.Data.Common;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    public class LocalTimeTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static LocalTimeTypeMapping Default { get; } = new();

        public LocalTimeTypeMapping()
            : base(CreateRelationalTypeMappingParameters())
        {
        }

        protected LocalTimeTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        {
            return new LocalTimeTypeMapping(parameters);
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

            // Workaround for a SQLClient bug
            if (DbType == System.Data.DbType.Time)
            {
                ((SqlParameter)parameter).SqlDbType = SqlDbType.Time;
            }

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
        protected override string SqlLiteralFormatString => "'{0}'";

        private static RelationalTypeMappingParameters CreateRelationalTypeMappingParameters()
        {
            return new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    typeof(LocalTime),
                    new LocalTimeValueConverter()),
                SqlServerDateTimeTypes.Time,
                // Without this, HasPrecision(n) is ignored and the column is always time(7).
                StoreTypePostfix.Precision);
        }
    }
}
