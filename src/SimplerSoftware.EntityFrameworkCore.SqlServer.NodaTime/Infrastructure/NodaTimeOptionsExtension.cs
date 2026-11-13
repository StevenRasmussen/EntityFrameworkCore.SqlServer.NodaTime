using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Properties;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore.SqlServer.Infrastructure
{
    internal class NodaTimeOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo _info;

        public NodaTimeOptionsExtension()
        {
        }

        private NodaTimeOptionsExtension(NodaTimeOptionsExtension copyFrom)
        {
            DateTimeZoneProvider = copyFrom.DateTimeZoneProvider;
        }

        /// <summary>Looks up the IDs of stored <see cref="DateTimeZone"/> values (#22). tzdb unless set otherwise.</summary>
        public IDateTimeZoneProvider DateTimeZoneProvider { get; private init; } = DateTimeZoneProviders.Tzdb;

        public NodaTimeOptionsExtension WithDateTimeZoneProvider(IDateTimeZoneProvider provider)
            => new(this) { DateTimeZoneProvider = provider ?? throw new ArgumentNullException(nameof(provider)) };

        /// <summary>Adds the extension to the options, or updates it, applying the configuration given to UseNodaTime.</summary>
        public static void AddOrUpdate(DbContextOptionsBuilder optionsBuilder, Action<NodaTimeDbContextOptionsBuilder> nodaTimeOptionsAction)
        {
            var extension = optionsBuilder.Options.FindExtension<NodaTimeOptionsExtension>() ?? new NodaTimeOptionsExtension();

            if (nodaTimeOptionsAction != null)
            {
                var nodaTimeOptions = new NodaTimeDbContextOptionsBuilder(extension.DateTimeZoneProvider);
                nodaTimeOptionsAction(nodaTimeOptions);
                extension = extension.WithDateTimeZoneProvider(nodaTimeOptions.DateTimeZoneProvider);
            }

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        }

        // The time zone provider reaches the services through INodaTimeSingletonOptions, which also works with
        // UseInternalServiceProvider, where this isn't called.
        public void ApplyServices(IServiceCollection services)
            => services.AddNodaTime();

        public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

        public void Validate(IDbContextOptions options)
        {
            var internalServiceProvider = options.FindExtension<CoreOptionsExtension>()?.InternalServiceProvider;
            if (internalServiceProvider != null)
            {
                // AddNodaTime registers these with the type mappings. Resolving the type mappings themselves here would
                // create them before EF Core initializes the singleton options, with the default time zone provider.
                if (internalServiceProvider.GetService<INodaTimeSingletonOptions>() == null)
                    throw new InvalidOperationException(Resources.ServicesMissing);
            }
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension)
            {
            }

            private new NodaTimeOptionsExtension Extension => (NodaTimeOptionsExtension)base.Extension;

            public override bool IsDatabaseProvider => false;

            // The provider is part of the internal services (the type mappings use it), so contexts using different
            // providers need different service providers.
            public override int GetServiceProviderHashCode() => RuntimeHelpers.GetHashCode(Extension.DateTimeZoneProvider);

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["SqlServer:" + nameof(SqlServerDbContextOptionsBuilderExtensions.UseNodaTime)] = "1";
                debugInfo["SqlServer:" + nameof(NodaTimeDbContextOptionsBuilder.UseDateTimeZoneProvider)]
                    = RuntimeHelpers.GetHashCode(Extension.DateTimeZoneProvider).ToString();
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
                => other is ExtensionInfo otherInfo && ReferenceEquals(Extension.DateTimeZoneProvider, otherInfo.Extension.DateTimeZoneProvider);

            public override string LogFragment
                => ReferenceEquals(Extension.DateTimeZoneProvider, DateTimeZoneProviders.Tzdb)
                    ? "using NodaTime "
                    : $"using NodaTime with time zones from {Extension.DateTimeZoneProvider.VersionId} ";
        }
    }
}
