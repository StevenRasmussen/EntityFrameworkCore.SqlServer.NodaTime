using NodaTime;
using System.Collections.Generic;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    /// <summary>
    /// NodaTime values stored as JSON: lists (EF Core "primitive collections", each stored as a JSON array in
    /// one column) and a complex type mapped to a JSON column.
    /// </summary>
    public class NodaTimeCollections
    {
        public int Id { get; set; }

        public List<LocalDate> Dates { get; set; } = [];

        public List<LocalTime> Times { get; set; } = [];

        public List<LocalDateTime> LocalDateTimes { get; set; } = [];

        public List<Instant> Instants { get; set; } = [];

        public List<OffsetDateTime> OffsetDateTimes { get; set; } = [];

        public List<Duration> Durations { get; set; } = [];

        public NodaTimeJson Json { get; set; } = new();
    }

    public class NodaTimeJson
    {
        public LocalDate Date { get; set; }

        public LocalDate? NullableDate { get; set; }

        public LocalTime Time { get; set; }

        public LocalDateTime LocalDateTime { get; set; }

        public Instant Instant { get; set; }

        public OffsetDateTime OffsetDateTime { get; set; }

        public Duration Duration { get; set; }
    }
}
