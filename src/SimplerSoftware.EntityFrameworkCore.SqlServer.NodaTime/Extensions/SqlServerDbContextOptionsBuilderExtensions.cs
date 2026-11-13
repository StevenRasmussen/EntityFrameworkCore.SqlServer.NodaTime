using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure;
using System;

namespace Microsoft.EntityFrameworkCore
{
    public static class SqlServerDbContextOptionsBuilderExtensions
    {
        /// <summary>
        /// Enable NodaTime mappings.
        /// </summary>
        /// <param name="optionsBuilder">The build being used to configure SQL Server.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static SqlServerDbContextOptionsBuilder UseNodaTime(
            this SqlServerDbContextOptionsBuilder optionsBuilder)
            => UseNodaTime(optionsBuilder, nodaTimeOptionsAction: null);

        /// <summary>
        /// Enable NodaTime mappings, with options: for example
        /// <c>UseNodaTime(n =&gt; n.UseDateTimeZoneProvider(DateTimeZoneProviders.Bcl))</c>.
        /// </summary>
        /// <param name="optionsBuilder">The build being used to configure SQL Server.</param>
        /// <param name="nodaTimeOptionsAction">Configures the NodaTime options.</param>
        /// <returns>The options builder so that further configuration can be chained.</returns>
        public static SqlServerDbContextOptionsBuilder UseNodaTime(
            this SqlServerDbContextOptionsBuilder optionsBuilder,
            Action<NodaTimeDbContextOptionsBuilder> nodaTimeOptionsAction)
        {
            NodaTimeOptionsExtension.AddOrUpdate(((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder, nodaTimeOptionsAction);
            return optionsBuilder;
        }
    }
}

namespace Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions
{
    public static class SqlServerDbContextOptionsBuilderExtensions
    {
        [Obsolete("Please use the 'UseNodaTime' extension method found in the 'Microsoft.EntityFrameworkCore' namespace.")]
        public static SqlServerDbContextOptionsBuilder UseNodaTime(
            this SqlServerDbContextOptionsBuilder optionsBuilder)
        {
            return EntityFrameworkCore.SqlServerDbContextOptionsBuilderExtensions.UseNodaTime(optionsBuilder);
        }
    }
}
