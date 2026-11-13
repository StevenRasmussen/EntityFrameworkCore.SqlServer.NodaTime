using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.Storage;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNodaTime(this IServiceCollection serviceCollection)
        {
            new EntityFrameworkRelationalServicesBuilder(serviceCollection)
                .TryAdd<IRelationalTypeMappingSourcePlugin, SqlServerNodaTimeTypeMappingSourcePlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, InstantMethodCallTranslatorPlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, LocalDateMethodCallTranslatorPlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, LocalTimeMethodCallTranslatorPlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, DurationMethodCallTranslatorPlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, LocalDateTimeMethodCallTranslatorPlugin>()
                .TryAdd<IMethodCallTranslatorPlugin, OffsetDateTimeMethodCallTranslatorPlugin>()
                // Rewrites LocalDate + LocalTime so it can be translated (#48).
                .TryAdd<IInterceptor, NodaTimeQueryExpressionInterceptor>()
                // Maps ZonedDateTime properties as complex types (#22).
                .TryAdd<IConventionSetPlugin, NodaTimeConventionSetPlugin>()
                // Options the singleton services use, such as the time zone provider (#22).
                .TryAdd<ISingletonOptions, INodaTimeSingletonOptions>(p => p.GetRequiredService<INodaTimeSingletonOptions>())
                .TryAddProviderSpecificServices(b => b.TryAddSingleton<INodaTimeSingletonOptions, NodaTimeSingletonOptions>());

            // Track the following to see if we can use the 'TryAdd' method above for this at some point: https://github.com/dotnet/efcore/issues/26071
            serviceCollection.AddScoped<IMemberTranslatorProvider, NodaTimeTypesMemberTranslatorPlugin>();

            return serviceCollection;
        }
    }
}
