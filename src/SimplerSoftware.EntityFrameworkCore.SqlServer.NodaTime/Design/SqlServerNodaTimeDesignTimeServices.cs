using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Storage;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design
{
    [UsedImplicitly]
    public class SqlServerNodaTimeDesignTimeServices : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddSingleton<IRelationalTypeMappingSourcePlugin, SqlServerNodaTimeTypeMappingSourcePlugin>()
                // Only answers lookups by store type alone, which the plugin above leaves to EF Core.
                .AddSingleton<IRelationalTypeMappingSourcePlugin, SqlServerNodaTimeScaffoldingTypeMappingSourcePlugin>()
                .AddSingleton<IProviderCodeGeneratorPlugin, SqlServerNodaTimeCodeGeneratorPlugin>();
        }
    }
}
