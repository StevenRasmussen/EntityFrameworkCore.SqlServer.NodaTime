using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using IModel = Microsoft.EntityFrameworkCore.Metadata.IModel;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// A compiled model (what 'dotnet ef dbcontext optimize' generates) of the whole test model: the generated code has
    /// to compile, and reads and queries through it have to give the same results as the normal model. The model has
    /// every NodaTime type with its default mapping, explicit column types and precisions, every Duration storage (#38),
    /// lists stored as JSON and a JSON column, so their converters and JSON readers/writers all have to be generated
    /// correctly too.
    /// </summary>
    public class CompiledModelTests : QueryTestBase
    {
        public CompiledModelTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        /// <summary>The same tables and configuration as <see cref="RacingContext"/>, without the seed data.</summary>
        public sealed class CompiledModelContext(DbContextOptions<CompiledModelContext> options) : DbContext(options)
        {
            public DbSet<SupportedNodaTypes> TypeResults { get; set; }

            public DbSet<PrecisionNodaTypes> PrecisionTypes { get; set; }

            public DbSet<Race> Race { get; set; }

            public DbSet<RaceResult> RaceResult { get; set; }

            public DbSet<RaceSplit> RaceSplit { get; set; }

            public DbSet<NodaTimeCollections> NodaTimeCollections { get; set; }

            public DbSet<DurationStorageTypes> DurationStorageTypes { get; set; }

            public DbSet<ZonedEvent> ZonedEvents { get; set; }

            public DbSet<PeriodEvent> PeriodEvents { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
                => RacingContext.ConfigureMappings(modelBuilder);
        }

        private static readonly Lazy<IModel> _compiledModel = new(GenerateAndCompileModel);

        /// <summary>Generates the compiled model's code as 'dotnet ef dbcontext optimize' does, compiles it and loads it.</summary>
        private static IModel GenerateAndCompileModel()
        {
            using var context = new CompiledModelContext(
                new DbContextOptionsBuilder<CompiledModelContext>().UseSqlServer("Server=offline", x => x.UseNodaTime()).Options);

            var services = new ServiceCollection()
                .AddEntityFrameworkDesignTimeServices()
                .AddDbContextDesignTimeServices(context);

            // The provider's design-time services, found the way 'dotnet ef' finds them, then this library's.
            var providerAssembly = typeof(SqlServerDbContextOptionsExtensions).Assembly;
            var providerServicesType = providerAssembly.GetType(providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName, throwOnError: true);
            ((IDesignTimeServices)Activator.CreateInstance(providerServicesType!)!).ConfigureDesignTimeServices(services);
            new SqlServerNodaTimeDesignTimeServices().ConfigureDesignTimeServices(services);
            using var serviceProvider = services.BuildServiceProvider();

            var options = new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(CompiledModelContext),
                ModelNamespace = "CompiledModelTestsGenerated",
                Language = "C#",
            };
            var generator = serviceProvider.GetRequiredService<ICompiledModelCodeGeneratorSelector>().Select(options);
            var files = generator.GenerateModel(context.GetService<IDesignTimeModel>().Model, options);

            var modelType = GeneratedCode.Compile("CompiledModelTestsGenerated", files).GetType($"{options.ModelNamespace}.{nameof(CompiledModelContext)}Model");
            return (IModel)modelType!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        }

        private CompiledModelContext CreateContext()
            => new(new DbContextOptionsBuilder<CompiledModelContext>()
                .UseSqlServer(this.SqlConnection.ConnectionString, x => x.UseNodaTime())
                .UseModel(_compiledModel.Value)
                .Options);

        private static string Format(object row)
            => string.Join(", ", row.GetType().GetProperties().OrderBy(p => p.Name).Select(p => $"{p.Name}={FormatValue(p.GetValue(row))}"));

        private static string FormatValue(object value) => value switch
        {
            null => "null",
            Instant or LocalDateTime or OffsetDateTime or LocalDate or LocalTime or Duration => Iso(value),
            ZonedDateTime zoned => global::NodaTime.Text.ZonedDateTimePattern.ExtendedFormatOnlyIso.Format(zoned),
            DateTimeZone zone => zone.Id,
            Schedule schedule => Format(schedule),
            Period period => period.ToString(),
            PeriodRule rule => rule.Name + " " + FormatValue(rule.Every),
            _ => value.ToString(),
        };

        /// <summary>Reads every row of a table with both models.</summary>
        private async Task AssertSameRows<T>(Func<DbContext, IQueryable<T>> table, Func<T, int> id)
            where T : class
        {
            using var db = CreateContext();
            var expected = (await table(this.Db).AsNoTracking().ToListAsync()).OrderBy(id).Select(Format).ToList();
            var actual = (await table(db).AsNoTracking().ToListAsync()).OrderBy(id).Select(Format).ToList();

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
        }

        /// <summary>Runs a query with both models.</summary>
        private async Task AssertSameResults<T>(Func<IQueryable<DurationStorageTypes>, IQueryable<T>> query)
        {
            using var db = CreateContext();
            var expected = await query(this.Db.DurationStorageTypes).ToListAsync();
            var actual = await query(db.DurationStorageTypes).ToListAsync();

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Model_is_compiled()
        {
            using var db = CreateContext();

            Assert.Same(_compiledModel.Value, db.Model);
        }

        [Fact]
        public async Task Rows_read_the_same_as_with_the_normal_model()
        {
            await AssertSameRows(db => db.Set<SupportedNodaTypes>(), x => x.Id);
            await AssertSameRows(db => db.Set<PrecisionNodaTypes>(), x => x.Id);
            await AssertSameRows(db => db.Set<Race>(), x => x.Id);
            await AssertSameRows(db => db.Set<RaceResult>(), x => x.Id);
            await AssertSameRows(db => db.Set<RaceSplit>(), x => x.Id);
            await AssertSameRows(db => db.Set<DurationStorageTypes>(), x => x.Id);
            await AssertSameRows(db => db.Set<ZonedEvent>(), x => x.Id);
            await AssertSameRows(db => db.Set<PeriodEvent>(), x => x.Id);
        }

        /// <summary>A Period is created with its internal constructor, which the compiled model has to reach too (#3).</summary>
        [Fact]
        public async Task Period_queries_give_the_same_results_as_with_the_normal_model()
        {
            var period = Period.FromMonths(1) + Period.FromHours(2);
            using var db = CreateContext();

            async Task AssertSame<T>(Func<IQueryable<PeriodEvent>, IQueryable<T>> query)
                => Assert.Equal(
                    (await query(this.Db.PeriodEvents).ToListAsync()).Select(v => FormatValue(v)),
                    (await query(db.Set<PeriodEvent>()).ToListAsync()).Select(v => FormatValue(v)));

            await AssertSame(q => q.OrderBy(x => x.Id).Select(x => x.Every));
            await AssertSame(q => q.OrderBy(x => x.Id).Select(x => x.Rule.Every));
            await AssertSame(q => q.OrderBy(x => x.Id).Select(x => x.From + x.Every));
            await AssertSame(q => q.Where(x => x.Every.HasTimeComponent).OrderBy(x => x.Id).Select(x => x.Id));
            await AssertSame(q => q.OrderBy(x => x.Id).Select(x => x.From + period));
        }

        /// <summary>A ZonedDateTime is mapped through its private fields, which the compiled model has to reach too (#22).</summary>
        [Fact]
        public async Task ZonedDateTime_queries_give_the_same_results_as_with_the_normal_model()
        {
            var instant = Instant.FromUtc(2024, 6, 1, 0, 0);
            var paris = DateTimeZoneProviders.Tzdb["Europe/Paris"];
            using var db = CreateContext();

            async Task AssertSame<T>(Func<IQueryable<ZonedEvent>, IQueryable<T>> query)
                => Assert.Equal(
                    (await query(this.Db.ZonedEvents).ToListAsync()).Select(v => FormatValue(v)),
                    (await query(db.Set<ZonedEvent>()).ToListAsync()).Select(v => FormatValue(v)));

            await AssertSame(q => q.Where(x => x.Start.ToInstant() > instant).OrderBy(x => x.Id).Select(x => x.Id));
            await AssertSame(q => q.OrderBy(x => x.Start.ToInstant()).Select(x => x.Start));
            await AssertSame(q => q.Where(x => x.Start.Zone == paris).OrderBy(x => x.Id).Select(x => x.Start.LocalDateTime));
            await AssertSame(q => q.OrderBy(x => x.Id).Select(x => x.Start.Offset));
        }

        [Fact]
        public async Task Queries_give_the_same_results_as_with_the_normal_model()
        {
            var values = new[] { Duration.FromHours(25), -Duration.FromMilliseconds(1500) };
            var hundredDays = Duration.FromDays(100) + Duration.Epsilon;

            // Contains sends the list as JSON, written by each mapping's JSON writer.
            await AssertSameResults(q => q.Where(x => values.Contains(x.BigInt.Value)).OrderBy(x => x.Id).Select(x => x.Id));
            await AssertSameResults(q => q.Where(x => values.Contains(x.DateTime2.Value)).OrderBy(x => x.Id).Select(x => x.Id));
            await AssertSameResults(q => q.Where(x => values.Contains(x.Text.Value)).OrderBy(x => x.Id).Select(x => x.Id));
            await AssertSameResults(q => q.Where(x => x.Text == hundredDays).Select(x => x.Id));
            await AssertSameResults(q => q.Where(x => x.DateTime2 != null).OrderBy(x => x.Id).Select(x => x.DateTime2.Value.Hours));
            await AssertSameResults(q => q.Where(x => x.Int.Value.Hours == 1 && x.BigInt == x.Int).OrderBy(x => x.Id).Select(x => x.Id));
            await AssertSameResults(q => q.Where(x => x.Time != null).OrderBy(x => x.Id).Select(x => x.Time.Value + x.Time.Value));
        }

        [Fact]
        public async Task Json_round_trips_as_with_the_normal_model()
        {
            using var db = CreateContext();
            using var transaction = await db.Database.BeginTransactionAsync();
            var row = new NodaTimeCollections
            {
                Dates = [new LocalDate(2019, 2, 1)],
                Times = [new LocalTime(10, 1, 1, 1)],
                LocalDateTimes = [new LocalDateTime(2019, 2, 1, 10, 1, 1, 1)],
                Instants = [Instant.FromUtc(2019, 2, 1, 10, 0)],
                OffsetDateTimes = [new OffsetDateTime(new LocalDateTime(2019, 2, 1, 10, 0), Offset.FromHoursAndMinutes(5, 30))],
                Durations = [Duration.FromMinutes(10)],
                DurationsInNanoseconds = [Duration.FromHours(25), -Duration.FromMilliseconds(1500)],
                Json = new NodaTimeJson
                {
                    Date = new LocalDate(2019, 2, 1),
                    Time = new LocalTime(10, 1, 1, 1),
                    LocalDateTime = new LocalDateTime(2019, 2, 1, 10, 1, 1, 1),
                    Instant = Instant.FromUtc(2019, 2, 1, 10, 0),
                    OffsetDateTime = new OffsetDateTime(new LocalDateTime(2019, 2, 1, 10, 0), Offset.FromHoursAndMinutes(5, 30)),
                    Duration = Duration.FromMinutes(10),
                    DurationAsBigInt = Duration.FromHours(25),
                    DurationAsDateTime2 = -Duration.FromHours(26),
                    DurationAsText = Duration.FromDays(100),
                },
            };
            db.NodaTimeCollections.Add(row);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var readBack = await db.NodaTimeCollections.SingleAsync(x => x.Id == row.Id);

            Assert.Equal(Format(row.Json), Format(readBack.Json));
            Assert.Equal(row.DurationsInNanoseconds, readBack.DurationsInNanoseconds);
            Assert.Equal(row.Instants, readBack.Instants);
            Assert.Equal(row.OffsetDateTimes, readBack.OffsetDateTimes);
            Assert.Equal(
                [row.Id],
                await db.NodaTimeCollections.Where(x => x.Id == row.Id && x.DurationsInNanoseconds.Contains(Duration.FromHours(25)) && x.Json.DurationAsBigInt > Duration.FromDays(1)).Select(x => x.Id).ToListAsync());
        }
    }
}
