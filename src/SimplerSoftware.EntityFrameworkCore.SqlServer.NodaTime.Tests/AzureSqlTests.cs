using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using System.Linq;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>UseAzureSql(...).UseNodaTime() sets NodaTime up as UseSqlServer(...).UseNodaTime() does. No database connection is opened.</summary>
    [Trait("Category", "Offline")]
    public class AzureSqlTests
    {
        private static RacingContext Context(Action<AzureSqlDbContextOptionsBuilder> azureSql)
            => new(new DbContextOptionsBuilder<RacingContext>().UseAzureSql("Server=offline;Database=Offline", azureSql).Options);

        [Fact]
        public void UseNodaTime()
        {
            using var db = Context(x => x.UseNodaTime());
            var instant = Instant.FromUtc(2024, 6, 1, 0, 0);

            Assert.Same(DateTimeZoneProviders.Tzdb, db.GetService<INodaTimeSingletonOptions>().DateTimeZoneProvider);
            Assert.Contains("WHERE [z].[Start] > @instant", db.ZonedEvents.Where(x => x.Start.ToInstant() > instant).ToQueryString());
            // Azure SQL's JSON_VALUE says the type it returns, which SQL Server's doesn't: so this is Azure SQL's translation.
            Assert.Contains("JSON_VALUE([p].[Every], '$.Days' RETURNING int)", db.PeriodEvents.Select(x => x.Every.Days).ToQueryString());
        }

        [Fact]
        public void UseNodaTime_with_options()
        {
            using var db = Context(x => x.UseNodaTime(n => n.UseDateTimeZoneProvider(DateTimeZoneProviders.Bcl)));

            Assert.Same(DateTimeZoneProviders.Bcl, db.GetService<INodaTimeSingletonOptions>().DateTimeZoneProvider);
        }
    }
}
