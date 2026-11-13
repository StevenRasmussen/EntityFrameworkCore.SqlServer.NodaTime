using NodaTime;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    /// <summary>
    /// A Duration stored each of the supported ways (#38). Time is null where the value can't be a time
    /// (24 hours or more, or negative), and Int where it is outside the range of an int of seconds.
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
    }
}
