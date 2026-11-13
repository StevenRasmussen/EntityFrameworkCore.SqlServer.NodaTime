using NodaTime;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    /// <summary>
    /// A Duration stored each of the supported ways (#38). Time is null where the value can't be a time
    /// (24 hours or more, or negative), and Int where it is outside the range of an int of seconds.
    /// DateTime2 and Text hold the same values as BigInt.
    /// </summary>
    public class DurationStorageTypes
    {
        public int Id { get; set; }

        /// <summary>time(7), the default.</summary>
        public Duration? Time { get; set; }

        /// <summary>bigint, nanoseconds.</summary>
        public Duration? BigInt { get; set; }

        /// <summary>int, whole seconds.</summary>
        public Duration? Int { get; set; }

        /// <summary>datetime2, the offset from 1900-01-01.</summary>
        public Duration? DateTime2 { get; set; }

        /// <summary>nvarchar(28), fixed-width text.</summary>
        public Duration? Text { get; set; }
    }
}
