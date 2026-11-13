using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using NodaTime.Text;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// Stores a <see cref="Duration"/> in a string column as fixed-width text, with every nanosecond:
    /// <c>00000001:01:00:00.000000000</c> for 25 hours, <c>-00000001:02:03:00.500000000</c> for -26:03:00.5.
    /// That's days (8 digits), hours, minutes, seconds and nanoseconds, with a minus sign only for a negative value;
    /// at most 28 characters. Because every value has the same width, text order matches duration order for values
    /// that aren't negative.
    /// </summary>
    public class DurationStringValueConverter : ValueConverter<Duration, string>
    {
        /// <summary>The longest text a Duration is stored as.</summary>
        public const int MaxLength = 28;

        private static readonly DurationPattern Pattern = DurationPattern.CreateWithInvariantCulture("-DDDDDDDD:hh:mm:ss.fffffffff");

        public DurationStringValueConverter()
            : base(d => toProvider(d), s => fromProvider(s))
        {
        }

        public static string toProvider(Duration duration)
        {
            return Pattern.Format(duration);
        }

        /// <exception cref="UnparsableValueException">The text isn't in the stored format.</exception>
        public static Duration fromProvider(string text)
        {
            // A char or nchar column pads the value with spaces.
            return Pattern.Parse(text.Trim()).Value;
        }
    }
}
