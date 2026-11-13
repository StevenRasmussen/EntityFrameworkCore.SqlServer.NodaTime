using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System.Globalization;
using System.Text.Json;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// A <see cref="Period"/> as the JSON a Period column holds: {"Years":1,"Months":0,...}, with all ten units (#3).
    /// </summary>
    public class PeriodJsonValueConverter : ValueConverter<Period, string>
    {
        public PeriodJsonValueConverter()
            : base(period => toProvider(period), json => fromProvider(json))
        {
        }

        public static string toProvider(Period period)
            => string.Create(
                CultureInfo.InvariantCulture,
                $"{{\"Years\":{period.Years},\"Months\":{period.Months},\"Weeks\":{period.Weeks},\"Days\":{period.Days},\"Hours\":{period.Hours},\"Minutes\":{period.Minutes},\"Seconds\":{period.Seconds},\"Milliseconds\":{period.Milliseconds},\"Ticks\":{period.Ticks},\"Nanoseconds\":{period.Nanoseconds}}}");

        public static Period fromProvider(string json)
        {
            using var document = JsonDocument.Parse(json);
            var units = document.RootElement;
            long Unit(string name) => units.TryGetProperty(name, out var value) ? value.GetInt64() : 0;
            return new PeriodBuilder
            {
                Years = checked((int)Unit("Years")), Months = checked((int)Unit("Months")), Weeks = checked((int)Unit("Weeks")), Days = checked((int)Unit("Days")),
                Hours = Unit("Hours"), Minutes = Unit("Minutes"), Seconds = Unit("Seconds"), Milliseconds = Unit("Milliseconds"),
                Ticks = Unit("Ticks"), Nanoseconds = Unit("Nanoseconds"),
            }.Build();
        }
    }

    /// <summary>
    /// A <see cref="Period"/> sent to SQL Server as JSON, for a period that a query adds to a column, as in
    /// x.Start + period, so the SQL can read its units with JSON_VALUE (#3). Only used for such a value: a Period property
    /// is mapped as a complex type, which is stored in a JSON column of the same shape.
    /// </summary>
    public class PeriodJsonTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static PeriodJsonTypeMapping Default { get; } = new();

        public PeriodJsonTypeMapping()
            : base(new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(typeof(Period), new PeriodJsonValueConverter()),
                "nvarchar(max)",
                StoreTypePostfix.None,
                System.Data.DbType.String,
                unicode: true))
        {
        }

        protected PeriodJsonTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new PeriodJsonTypeMapping(parameters);

        protected override string GenerateNonNullSqlLiteral(object value)
            => "N'" + ((string)value).Replace("'", "''") + "'";
    }
}
