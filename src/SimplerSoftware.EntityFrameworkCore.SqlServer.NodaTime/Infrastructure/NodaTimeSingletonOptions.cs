using Microsoft.EntityFrameworkCore.Infrastructure;
using NodaTime;
using System;

namespace Microsoft.EntityFrameworkCore.SqlServer.Infrastructure
{
    /// <summary>
    ///     The NodaTime options the internal (singleton) services are built with, such as the type mappings.
    /// </summary>
    /// <remarks>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </remarks>
    public interface INodaTimeSingletonOptions : ISingletonOptions
    {
        /// <summary>The provider stored time zone IDs are looked up in (#22).</summary>
        IDateTimeZoneProvider DateTimeZoneProvider { get; }
    }

    /// <summary>
    /// EF Core initializes this from the first context's options, before the services that use it are created, and checks
    /// every later context's options against it. That also covers UseInternalServiceProvider, where the options don't add
    /// services, and where a different provider in a later context would otherwise go unnoticed.
    /// </summary>
    internal sealed class NodaTimeSingletonOptions : INodaTimeSingletonOptions
    {
        public IDateTimeZoneProvider DateTimeZoneProvider { get; private set; } = DateTimeZoneProviders.Tzdb;

        public void Initialize(IDbContextOptions options)
            => DateTimeZoneProvider = ProviderIn(options);

        public void Validate(IDbContextOptions options)
        {
            if (!ReferenceEquals(ProviderIn(options), DateTimeZoneProvider))
            {
                throw new InvalidOperationException(
                    $"A call was made to '{nameof(NodaTimeDbContextOptionsBuilder.UseDateTimeZoneProvider)}' that changed the time zone "
                    + "provider, which must be the same for every context using a given service provider. Entity Framework isn't "
                    + $"building its own internal service provider: either remove the call to '{nameof(DbContextOptionsBuilder.UseInternalServiceProvider)}', "
                    + "or use the same time zone provider with every context that shares it.");
            }
        }

        private static IDateTimeZoneProvider ProviderIn(IDbContextOptions options)
            => options.FindExtension<NodaTimeOptionsExtension>()?.DateTimeZoneProvider ?? DateTimeZoneProviders.Tzdb;
    }
}
