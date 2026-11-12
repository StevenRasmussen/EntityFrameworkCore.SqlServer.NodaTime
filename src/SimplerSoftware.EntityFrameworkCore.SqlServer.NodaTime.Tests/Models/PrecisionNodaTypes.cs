using NodaTime;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    /// <summary>
    /// Properties mapped to non-default store types, configured in <see cref="RacingContext"/>.
    /// "As..." properties use HasColumnType, "With..." properties use HasPrecision.
    /// </summary>
    public class PrecisionNodaTypes
    {
        public int Id { get; set; }

        public Instant InstantAsDateTime { get; set; }
        public Instant InstantAsSmallDateTime { get; set; }
        public Instant InstantAsDateTime2Of0 { get; set; }
        public Instant InstantWithPrecision3 { get; set; }

        public LocalDateTime LocalDateTimeAsDateTime { get; set; }
        public LocalDateTime LocalDateTimeAsSmallDateTime { get; set; }
        public LocalDateTime LocalDateTimeAsDateTime2Of0 { get; set; }
        public LocalDateTime LocalDateTimeWithPrecision3 { get; set; }

        public OffsetDateTime OffsetDateTimeAsDateTimeOffsetOf3 { get; set; }
        public OffsetDateTime OffsetDateTimeWithPrecision2 { get; set; }

        public LocalTime LocalTimeAsTimeOf3 { get; set; }

        public Duration DurationAsTimeOf3 { get; set; }
    }
}
