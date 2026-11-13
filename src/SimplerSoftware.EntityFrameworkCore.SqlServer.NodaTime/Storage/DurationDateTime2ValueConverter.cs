using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// Stores a <see cref="Duration"/> in a datetime2 column as that long after 1900-01-01 00:00, the date SQL Server
    /// gives a time value (so 25 hours is 1900-01-02 01:00, and -1 hour is 1899-12-31 23:00). datetime2 keeps 100ns, so
    /// finer nanoseconds are truncated toward zero, as for time; the range is from about -1899 to +8099 years.
    /// </summary>
    public class DurationDateTime2ValueConverter : ValueConverter<Duration, DateTime>
    {
        /// <summary>1900-01-01 00:00, which a duration of zero is stored as.</summary>
        public static readonly DateTime Zero = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public DurationDateTime2ValueConverter()
            : base(d => toProvider(d), dt => fromProvider(dt))
        {
        }

        /// <exception cref="ArgumentOutOfRangeException">The duration is outside the range of a datetime2.</exception>
        public static DateTime toProvider(Duration duration)
        {
            var ticks = duration.BclCompatibleTicks;
            if (ticks < DateTime.MinValue.Ticks - Zero.Ticks || ticks > DateTime.MaxValue.Ticks - Zero.Ticks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "A Duration stored as datetime2 must be from about -1899 to +8099 years (0001-01-01 to 9999-12-31 as an offset from 1900-01-01).");
            }

            return Zero.AddTicks(ticks);
        }

        public static Duration fromProvider(DateTime dateTime)
        {
            return Duration.FromTicks(dateTime.Ticks - Zero.Ticks);
        }
    }
}
