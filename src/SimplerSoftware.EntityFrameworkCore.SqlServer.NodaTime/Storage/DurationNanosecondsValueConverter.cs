using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;

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
            try
            {
                return duration.ToInt64Nanoseconds();
            }
            catch (OverflowException exception)
            {
                throw new OverflowException($"A Duration stored as bigint (nanoseconds) must be within about ±292 years; {duration} isn't.", exception);
            }
        }

        public static Duration fromProvider(long nanoseconds)
        {
            return Duration.FromNanoseconds(nanoseconds);
        }
    }
}
