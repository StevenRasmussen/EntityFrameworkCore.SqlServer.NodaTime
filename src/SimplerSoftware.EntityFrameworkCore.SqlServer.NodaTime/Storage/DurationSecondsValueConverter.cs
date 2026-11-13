using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using System;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// Stores a <see cref="Duration"/> as a whole number of seconds, for an int column. Any fraction of a second is
    /// truncated toward zero (1.7s is stored as 1s, -1.7s as -1s); the range is about ±68 years.
    /// </summary>
    public class DurationSecondsValueConverter : ValueConverter<Duration, int>
    {
        public DurationSecondsValueConverter()
            : base(d => toProvider(d), s => fromProvider(s))
        {
        }

        /// <exception cref="System.OverflowException">The duration is outside the range of an int of seconds.</exception>
        public static int toProvider(Duration duration)
        {
            // Integer division truncates toward zero. A duration too long for nanoseconds in a long is far too long
            // for seconds in an int, so ToInt64Nanoseconds throwing for it is the right outcome too.
            try
            {
                return checked((int)(duration.ToInt64Nanoseconds() / NodaConstants.NanosecondsPerSecond));
            }
            catch (OverflowException exception)
            {
                throw new OverflowException($"A Duration stored as int (whole seconds) must be within about ±68 years; {duration} isn't.", exception);
            }
        }

        public static Duration fromProvider(int seconds)
        {
            return Duration.FromSeconds((long)seconds);
        }
    }
}
