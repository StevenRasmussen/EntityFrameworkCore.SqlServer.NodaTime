using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    public class DatabaseTestFixture
    {
        public DatabaseTestFixture()
        {
            this.SqlConnection = new SqlConnection(@"Server=(localdb)\MSSQLLocalDB;Database=NodaTimeTests;Trusted_Connection=True");

            this.DbContextOptions = new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer(this.SqlConnection, x => x.UseNodaTime())
                .Options;

            this.DbContext = new RacingContext(this.DbContextOptions);
            this.DbContext.Database.EnsureDeleted();
            this.DbContext.Database.EnsureCreated();

            // EF Core can't seed complex properties (the ZonedDateTime ones) with HasData.
            this.DbContext.ZonedEvents.AddRange(RacingContext.ZonedEventRows());
            this.DbContext.PeriodEvents.AddRange(RacingContext.PeriodEventRows());
            this.DbContext.SaveChanges();
            this.DbContext.ChangeTracker.Clear();
        }

        public RacingContext DbContext { get; }

        public DbContextOptions<RacingContext> DbContextOptions { get; }

        public SqlConnection SqlConnection { get; }
    }

    [CollectionDefinition(nameof(DatabaseTestCollection))]
    public class DatabaseTestCollection : ICollectionFixture<DatabaseTestFixture>
    {
    }
}
