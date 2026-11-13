using NodaTime;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    /// <summary>Period properties (#3), and dates and times to add them to and to measure periods between.</summary>
    public class PeriodEvent
    {
        public int Id { get; set; }

        /// <summary>A JSON column, required: mixed units, odd ones included.</summary>
        public Period Every { get; set; }

        /// <summary>A JSON column, optional: date units only.</summary>
        public Period Grace { get; set; }

        /// <summary>A JSON column, optional: time units only.</summary>
        public Period Shift { get; set; }

        public LocalDateTime From { get; set; }

        public LocalDateTime Until { get; set; }

        public LocalDate FromDate { get; set; }

        public LocalDate UntilDate { get; set; }

        public LocalTime At { get; set; }

        public LocalTime UntilTime { get; set; }

        /// <summary>A complex type of the application's own, with a Period inside: a column for each unit (Rule_Every_Years...).</summary>
        public PeriodRule Rule { get; set; } = new();
    }

    public class PeriodRule
    {
        public string Name { get; set; }

        public Period Every { get; set; }
    }
}
