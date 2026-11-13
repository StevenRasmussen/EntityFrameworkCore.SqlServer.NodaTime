using NodaTime;
using System;

namespace Microsoft.EntityFrameworkCore.Infrastructure
{
    /// <summary>Options for the NodaTime mappings, given to UseNodaTime(n =&gt; ...).</summary>
    public class NodaTimeDbContextOptionsBuilder
    {
        internal NodaTimeDbContextOptionsBuilder(IDateTimeZoneProvider dateTimeZoneProvider)
        {
            DateTimeZoneProvider = dateTimeZoneProvider;
        }

        internal IDateTimeZoneProvider DateTimeZoneProvider { get; private set; }

        /// <summary>
        /// The provider that stored time zone IDs are looked up in when <see cref="DateTimeZone"/> and
        /// <see cref="ZonedDateTime"/> values are read: <see cref="DateTimeZoneProviders.Tzdb"/> (the default, with IDs such
        /// as "Europe/Paris"), <see cref="DateTimeZoneProviders.Bcl"/> (the operating system's zones, with IDs such as
        /// "W. Europe Standard Time" on Windows), or any other. Store and read values with the same provider. A compiled
        /// model only supports NodaTime's two built-in providers.
        /// </summary>
        /// <remarks>
        /// Pass the same instance every time, as DateTimeZoneProviders' are: EF Core builds and keeps internal services for
        /// each provider instance, so a new one for every context would build them again and again.
        /// </remarks>
        public NodaTimeDbContextOptionsBuilder UseDateTimeZoneProvider(IDateTimeZoneProvider provider)
        {
            DateTimeZoneProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            return this;
        }
    }
}
