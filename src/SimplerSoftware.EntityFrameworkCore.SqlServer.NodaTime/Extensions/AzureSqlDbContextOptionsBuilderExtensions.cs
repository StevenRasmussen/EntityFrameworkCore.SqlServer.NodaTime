using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore;

public static class AzureSqlDbContextOptionsBuilderExtensions
{
    public static AzureSqlDbContextOptionsBuilder UseNodaTime(
        this AzureSqlDbContextOptionsBuilder azureBuilder)
        => UseNodaTime(azureBuilder, nodaTimeOptionsAction: null);

    /// <summary>
    /// Enable NodaTime mappings, with options: for example
    /// <c>UseNodaTime(n =&gt; n.UseDateTimeZoneProvider(DateTimeZoneProviders.Bcl))</c>.
    /// </summary>
    public static AzureSqlDbContextOptionsBuilder UseNodaTime(
        this AzureSqlDbContextOptionsBuilder azureBuilder,
        System.Action<NodaTimeDbContextOptionsBuilder> nodaTimeOptionsAction)
    {
        NodaTimeOptionsExtension.AddOrUpdate(((IRelationalDbContextOptionsBuilderInfrastructure)azureBuilder).OptionsBuilder, nodaTimeOptionsAction);
        return azureBuilder;
    }
}