using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Text;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// Period properties (#3): the model, without a database. <see cref="PeriodQueryTests"/> checks the results.
    /// </summary>
    [Trait("Category", "Offline")]
    public class PeriodMappingTests
    {
        public class Plan
        {
            public int Id { get; set; }

            public Period Every { get; set; }
        }

        private sealed class PlanContext(Action<ModelBuilder> configure, Action<ModelConfigurationBuilder> conventions = null) : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder options)
                => options.UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime()).EnableServiceProviderCaching(false);

            protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                => conventions?.Invoke(configurationBuilder);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Plan>();
                configure(modelBuilder);
            }
        }

        public class PeriodAsText() : ValueConverter<Period, string>(p => PeriodPattern.Roundtrip.Format(p), s => PeriodPattern.Roundtrip.Parse(s).Value);

        [Fact]
        public void Columns()
        {
            using var db = new RacingContext(new DbContextOptionsBuilder<RacingContext>().UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime()).Options);

            var script = db.Database.GenerateCreateScript();

            Assert.Contains("[Every] nvarchar(max) NOT NULL", script);
            Assert.Contains("[Grace] nvarchar(max) NULL", script);
            Assert.Contains("[Shift] nvarchar(max) NULL", script);
            // In a complex type stored in the table's own columns, where EF Core can't put a JSON column: one for each unit.
            Assert.Contains("[Rule_Every_Years] int NULL", script);
            Assert.Contains("[Rule_Every_Nanoseconds] bigint NULL", script);
            Assert.Equal(10, script.Split("[Rule_Every_").Length - 1);
        }

        [Fact]
        public void Json()
        {
            using var db = new PlanContext(_ => { });
            var complexType = db.Model.FindEntityType(typeof(Plan))!.FindComplexProperty(nameof(Plan.Every))!.ComplexType;

            Assert.Equal("Every", complexType.GetContainerColumnName());
            Assert.Equal(
                new[] { "Years", "Months", "Weeks", "Days", "Hours", "Minutes", "Seconds", "Milliseconds", "Ticks", "Nanoseconds" }.OrderBy(n => n),
                complexType.GetProperties().Select(p => p.GetJsonPropertyName()).OrderBy(n => n));
        }

        /// <summary>A model that already stored a Period its own way, with a value converter, keeps it.</summary>
        [Fact]
        public void Own_converter_takes_precedence()
        {
            using var db = new PlanContext(b => b.Entity<Plan>().Property(x => x.Every).HasConversion<PeriodAsText>());

            Assert.Contains("[Every] nvarchar(max) NULL", db.Database.GenerateCreateScript());
            Assert.Null(db.Model.FindEntityType(typeof(Plan))!.FindComplexProperty(nameof(Plan.Every)));
        }

        [Fact]
        public void Own_converter_in_ConfigureConventions_takes_precedence()
        {
            using var db = new PlanContext(_ => { }, c => c.Properties<Period>().HaveConversion<PeriodAsText>());

            Assert.Contains("[Every] nvarchar(max) NULL", db.Database.GenerateCreateScript());
            Assert.Null(db.Model.FindEntityType(typeof(Plan))!.FindComplexProperty(nameof(Plan.Every)));
        }

        [Fact]
        public void IgnoreAny_in_ConfigureConventions_ignores_it()
        {
            using var db = new PlanContext(_ => { }, c => c.IgnoreAny<Period>());

            Assert.DoesNotContain("Every", db.Database.GenerateCreateScript());
        }

        [Fact]
        public void Ignored_property_stays_ignored()
        {
            using var db = new PlanContext(b => b.Entity<Plan>().Ignore(x => x.Every));

            Assert.DoesNotContain("Every", db.Database.GenerateCreateScript());
        }

        public abstract class Audited
        {
            public int Id { get; set; }

            public Period RetainFor { get; set; }
        }

        public class Invoice : Audited
        {
        }

        [Fact]
        public void Inherited_property()
        {
            using var db = new PlanContext(b => b.Entity<Invoice>());

            Assert.Contains("[RetainFor] nvarchar(max) NULL", db.Database.GenerateCreateScript());
        }

        public class Lesson
        {
            public int Id { get; set; }

            public Booking Booking { get; set; }
        }

        public class Booking
        {
            public Period Length { get; set; }
        }

        /// <summary>EF Core can't query a complex property of an owned type, so the model fails to build, saying what to do.</summary>
        [Fact]
        public void Owned_type()
        {
            using var db = new PlanContext(b => b.Entity<Lesson>().OwnsOne(x => x.Booking));

            var exception = Assert.Throws<InvalidOperationException>(() => db.Model);

            Assert.Contains("'Booking.Length' is a Period on the owned entity type 'Booking'", exception.Message);
        }

        [Fact]
        public void Migration()
        {
            using var db = new PlanContext(_ => { });
            var services = new ServiceCollection()
                .AddEntityFrameworkDesignTimeServices()
                .AddDbContextDesignTimeServices(db);
            var providerAssembly = typeof(SqlServerDbContextOptionsExtensions).Assembly;
            var providerServicesType = providerAssembly.GetType(providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName, throwOnError: true);
            ((IDesignTimeServices)Activator.CreateInstance(providerServicesType!)!).ConfigureDesignTimeServices(services);
            new SqlServerNodaTimeDesignTimeServices().ConfigureDesignTimeServices(services);
            using var serviceProvider = services.BuildServiceProvider();

            var migration = serviceProvider.GetRequiredService<IMigrationsScaffolder>().ScaffoldMigration("Initial", "Tests");

            Assert.Contains("Every = table.Column<string>(type: \"nvarchar(max)\", nullable: true)", migration.MigrationCode);
            Assert.Contains("ComplexProperty(typeof(Dictionary<string, object>), \"Every\"", migration.SnapshotCode);
        }

        /// <summary>A parameter or constant period that a query adds to a column is sent as JSON of the same shape.</summary>
        [Fact]
        public void Parameter_json()
        {
            var period = new PeriodBuilder { Years = 1, Hours = -2, Nanoseconds = long.MaxValue }.Build();

            var json = PeriodJsonValueConverter.toProvider(period);

            Assert.Equal("""{"Years":1,"Months":0,"Weeks":0,"Days":0,"Hours":-2,"Minutes":0,"Seconds":0,"Milliseconds":0,"Ticks":0,"Nanoseconds":9223372036854775807}""", json);
            Assert.Equal(period, PeriodJsonValueConverter.fromProvider(json));
        }

        /// <summary>
        /// EF Core creates a Period with its internal constructor taking all ten units, so a NodaTime version without it
        /// would break the mapping. This fails first, with a clear reason.
        /// </summary>
        [Fact]
        public void NodaTime_has_the_constructor_the_mapping_uses()
        {
            var constructor = typeof(Period).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                [typeof(int), typeof(int), typeof(int), typeof(int), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long)]);

            Assert.NotNull(constructor);
            Assert.Equal(
                ["years", "months", "weeks", "days", "hours", "minutes", "seconds", "milliseconds", "ticks", "nanoseconds"],
                constructor.GetParameters().Select(p => p.Name));
        }
    }
}
