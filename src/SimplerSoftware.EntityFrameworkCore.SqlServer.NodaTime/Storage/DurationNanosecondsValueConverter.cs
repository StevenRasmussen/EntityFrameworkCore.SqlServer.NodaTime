using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// Stores a <see cref="Duration"/> as a whole number of nanoseconds, for a bigint column. NodaTime's own precision,
    /// so nothing is lost; the range is about ±292 years.
    /// </summary>
    public class DurationNanosecondsValueConverter : ValueConverter<Duration, long>
    {
        public DurationNanosecondsValueConverter()
            : base(d => toProvider(d), ns => fromProvider(ns))
        {
        }

        /// <exception cref="System.OverflowException">The duration is outside the range of a bigint of nanoseconds.</exception>
        public static long toProvider(Duration duration)
        {
            return duration.ToInt64Nanoseconds();
        }

        public static Duration fromProvider(long nanoseconds)
        {
            return Duration.FromNanoseconds(nanoseconds);
        }
    }
}
