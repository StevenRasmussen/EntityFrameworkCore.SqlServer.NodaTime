using NodaTime;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    /// <summary>ZonedDateTime, DateTimeZone and Offset properties (#22), and an OffsetDateTime to compare instants with.</summary>
    public class ZonedEvent
    {
        public int Id { get; set; }

        /// <summary>Columns Start (datetimeoffset) and Start_Zone, configured as char(32).</summary>
        public ZonedDateTime Start { get; set; }

        public ZonedDateTime? End { get; set; }

        public DateTimeZone HomeZone { get; set; }

        public Offset? UtcOffset { get; set; }

        public OffsetDateTime Booked { get; set; }

        /// <summary>A complex type of the application's own, with a ZonedDateTime inside: columns Schedule_Reminder and Schedule_Reminder_Zone.</summary>
        public Schedule Schedule { get; set; } = new();
    }

    public class Schedule
    {
        public ZonedDateTime? Reminder { get; set; }
    }
}
