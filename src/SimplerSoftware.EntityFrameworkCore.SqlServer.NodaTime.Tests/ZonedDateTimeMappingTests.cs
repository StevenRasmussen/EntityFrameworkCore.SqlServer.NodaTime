using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Storage;
using Microsoft.EntityFrameworkCore.Storage;
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
    /// ZonedDateTime, DateTimeZone and Offset (#22): the model, the mappings and the options. Without a database;
    /// <see cref="ZonedDateTimeQueryTests"/> checks the results.
    /// </summary>
    [Trait("Category", "Offline")]
    public class ZonedDateTimeMappingTests
    {
        private static RacingContext Context(Action<NodaTimeDbContextOptionsBuilder> nodaTime = null)
            => new(new DbContextOptionsBuilder<RacingContext>()
                .UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime(nodaTime))
                .Options);

        public class Appointment
        {
            public int Id { get; set; }

            public ZonedDateTime When { get; set; }
        }

        private sealed class AppointmentContext(Action<ModelBuilder> configure, Action<ModelConfigurationBuilder> conventions = null) : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder options)
                => options.UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime()).EnableServiceProviderCaching(false);

            protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                => conventions?.Invoke(configurationBuilder);

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Appointment>();
                configure(modelBuilder);
            }
        }

        /// <summary>Columns named the way EF Core names a complex property's, including inside the application's own complex type.</summary>
        [Fact]
        public void Columns()
        {
            using var db = Context();

            var script = db.Database.GenerateCreateScript();

            Assert.Contains("[Start] datetimeoffset NOT NULL", script);
            Assert.Contains("[Start_Zone] char(32) NOT NULL", script);
            Assert.Contains("[End] datetimeoffset NULL", script);
            Assert.Contains("[End_Zone] nvarchar(50) NULL", script);
            Assert.Contains("[Schedule_Reminder] datetimeoffset NULL", script);
            Assert.Contains("[Schedule_Reminder_Zone] nvarchar(50) NULL", script);
            Assert.Contains("[HomeZone] nvarchar(50) NULL", script);
            Assert.Contains("[UtcOffset] int NULL", script);
        }

        private sealed class OptionsContext(DbContextOptions<OptionsContext> options) : DbContext(options)
        {
            public DbSet<Appointment> Appointments { get; set; }
        }

        public abstract class Audited
        {
            public int Id { get; set; }

            public ZonedDateTime Created { get; set; }
        }

        public class Invoice : Audited
        {
        }

        public class Animal
        {
            public int Id { get; set; }

            public ZonedDateTime Born { get; set; }
        }

        public class Dog : Animal
        {
            public ZonedDateTime Walked { get; set; }
        }

        /// <summary>
        /// A ZonedDateTime on a base class that isn't an entity type belongs to the entity types that derive from it; on one
        /// that is, to the base entity type, whichever is added to the model first.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Inherited_properties(bool derivedFirst)
        {
            using var db = new AppointmentContext(b =>
            {
                b.Entity<Invoice>();
                if (derivedFirst)
                    b.Entity<Dog>();
                b.Entity<Animal>();
                b.Entity<Dog>();
            });

            var script = db.Database.GenerateCreateScript();

            Assert.Contains("[Created] datetimeoffset NOT NULL", script);
            Assert.Contains("[Created_Zone] nvarchar(50) NOT NULL", script);
            Assert.Contains("[Born] datetimeoffset NOT NULL", script);
            Assert.Contains("[Walked] datetimeoffset NULL", script);
            Assert.Equal(typeof(Animal), db.Model.FindEntityType(typeof(Dog))!.FindComplexProperty(nameof(Animal.Born))!.DeclaringType.ClrType);
        }

        [Theory]
        [InlineData(null, "nvarchar(50)", 50)]
        [InlineData("nvarchar", "nvarchar(50)", 50)]
        [InlineData("varchar(40)", "varchar(40)", 40)]
        [InlineData("NCHAR(32)", "NCHAR(32)", 32)]
        [InlineData("nvarchar(max)", "nvarchar(max)", -1)]
        public void DateTimeZone_column_type(string columnType, string expectedStoreType, int expectedParameterSize)
        {
            using var db = Context();
            var source = db.GetService<IRelationalTypeMappingSource>();

            var mapping = columnType == null ? source.FindMapping(typeof(DateTimeZone)) : source.FindMapping(typeof(DateTimeZone), columnType);
            using var command = new Microsoft.Data.SqlClient.SqlCommand();
            var parameter = mapping!.CreateParameter(command, "p", DateTimeZoneProviders.Tzdb["Europe/Paris"]);

            Assert.IsType<DateTimeZoneTypeMapping>(mapping);
            Assert.Equal(expectedStoreType, mapping.StoreType);
            Assert.Equal(expectedParameterSize, parameter.Size);
        }

        /// <summary>
        /// An ID longer than the column (a custom provider's, say) is sent as nvarchar(max), so that SQL Server refuses to
        /// save it, rather than with the column's length, which would make SqlClient cut it short.
        /// </summary>
        [Fact]
        public void DateTimeZone_longer_than_the_column()
        {
            using var command = new Microsoft.Data.SqlClient.SqlCommand();

            Assert.Equal(-1, DateTimeZoneTypeMapping.Default.CreateParameter(command, "p", new LongIdZone()).Size);
        }

        private sealed class LongIdZone() : DateTimeZone(new string('Z', 60), isFixed: true, Offset.Zero, Offset.Zero)
        {
            public override global::NodaTime.TimeZones.ZoneInterval GetZoneInterval(Instant instant) => Utc.GetZoneInterval(instant);
        }

        /// <summary>A char or nchar column pads the ID with spaces.</summary>
        [Fact]
        public void DateTimeZone_padded_with_spaces()
        {
            var paris = DateTimeZoneProviders.Tzdb["Europe/Paris"];

            Assert.Same(paris, new DateTimeZoneValueConverter().ConvertFromProvider("Europe/Paris" + new string(' ', 20)));
            Assert.Same(paris, new DateTimeZoneValueConverter(new TestProvider()).ConvertFromProvider("Europe/Paris   "));
        }

        private sealed class TestProvider : IDateTimeZoneProvider
        {
            public string VersionId => "Test";

            public System.Collections.ObjectModel.ReadOnlyCollection<string> Ids => DateTimeZoneProviders.Tzdb.Ids;

            public DateTimeZone GetSystemDefault() => DateTimeZone.Utc;

            public DateTimeZone GetZoneOrNull(string id) => DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);

            public DateTimeZone this[string id] => DateTimeZoneProviders.Tzdb[id];
        }

        [Fact]
        public void DateTimeZone_values()
        {
            var mapping = DateTimeZoneTypeMapping.Default;
            var paris = DateTimeZoneProviders.Tzdb["Europe/Paris"];

            Assert.Equal("N'Europe/Paris'", mapping.GenerateSqlLiteral(paris));
            Assert.Equal("\"Europe/Paris\"", mapping.JsonValueReaderWriter!.ToJsonString(paris));
            Assert.Same(paris, mapping.JsonValueReaderWriter.FromJsonString("\"Europe/Paris\""));
        }

        [Fact]
        public void Offset_values()
        {
            using var db = Context();
            var mapping = db.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(Offset));

            Assert.Equal("int", mapping.StoreType);
            Assert.Equal("19800", mapping.GenerateSqlLiteral(Offset.FromHoursAndMinutes(5, 30)));
            Assert.Equal("-14400", mapping.GenerateSqlLiteral(Offset.FromHours(-4)));
            // SQL Server's synonym for int.
            Assert.IsType<OffsetTypeMapping>(db.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(Offset), "integer"));
        }

        /// <summary>The provider is part of the options: tzdb by default, or another one, each with its own services.</summary>
        [Fact]
        public void DateTimeZone_provider_option()
        {
            using var tzdb = Context();
            using var bcl = Context(n => n.UseDateTimeZoneProvider(DateTimeZoneProviders.Bcl));
            var bclZone = DateTimeZoneProviders.Bcl.GetSystemDefault();

            Assert.Same(DateTimeZoneProviders.Tzdb, tzdb.GetService<INodaTimeSingletonOptions>().DateTimeZoneProvider);
            Assert.Same(DateTimeZoneProviders.Bcl, bcl.GetService<INodaTimeSingletonOptions>().DateTimeZoneProvider);
            var converter = (DateTimeZoneValueConverter)bcl.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(DateTimeZone))!.Converter!;
            Assert.Same(DateTimeZoneProviders.Bcl, converter.Provider);
            Assert.Equal(bclZone.Id, ((DateTimeZone)converter.ConvertFromProvider(bclZone.Id)!).Id);
        }

        /// <summary>
        /// With UseInternalServiceProvider, the options add no services, but the provider still comes from them. Every
        /// context using that service provider must use the same one.
        /// </summary>
        [Fact]
        public void DateTimeZone_provider_with_an_internal_service_provider()
        {
            using var serviceProvider = new ServiceCollection().AddEntityFrameworkSqlServer().AddNodaTime().BuildServiceProvider(validateScopes: true);
            OptionsContext Create(IDateTimeZoneProvider provider)
                => new(new DbContextOptionsBuilder<OptionsContext>()
                    .UseInternalServiceProvider(serviceProvider)
                    .UseSqlServer("Server=offline;Database=Offline", x => x.UseNodaTime(n => n.UseDateTimeZoneProvider(provider)))
                    .Options);

            using var bcl = Create(DateTimeZoneProviders.Bcl);
            var converter = (DateTimeZoneValueConverter)bcl.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(DateTimeZone))!.Converter!;
            Assert.Same(DateTimeZoneProviders.Bcl, converter.Provider);

            using var tzdb = Create(DateTimeZoneProviders.Tzdb);
            var exception = Assert.Throws<InvalidOperationException>(() => tzdb.GetService<IRelationalTypeMappingSource>());
            Assert.Contains(nameof(NodaTimeDbContextOptionsBuilder.UseDateTimeZoneProvider), exception.Message);
        }

        /// <summary>A model that already stored a ZonedDateTime its own way, with a value converter, keeps it.</summary>
        [Fact]
        public void Own_converter_takes_precedence()
        {
            var pattern = ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFFFo<G> z", DateTimeZoneProviders.Tzdb);
            using var db = new AppointmentContext(b => b.Entity<Appointment>().Property(x => x.When).HasConversion(z => pattern.Format(z), s => pattern.Parse(s).Value));

            var script = db.Database.GenerateCreateScript();

            Assert.Contains("[When] nvarchar(max) NOT NULL", script);
            Assert.DoesNotContain("When_Zone", script);
            // Its parts aren't the mapping's columns: EF Core evaluates them in .NET, from the value it reads.
            Assert.Contains("SELECT [a].[When]", db.Set<Appointment>().Select(x => x.When.ToInstant()).ToQueryString());
        }

        public class Lesson
        {
            public int Id { get; set; }

            public Booking Booking { get; set; }
        }

        public class Booking
        {
            public ZonedDateTime Start { get; set; }
        }

        /// <summary>
        /// EF Core can't query a complex property of an owned type, whatever its table: every query of the owner would
        /// fail inside EF Core. So the model fails to build, saying what to do instead.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Owned_type(bool ownTable)
        {
            using var db = new AppointmentContext(b => b.Entity<Lesson>().OwnsOne(x => x.Booking, o =>
            {
                if (ownTable)
                    o.ToTable("Bookings");
            }));

            var exception = Assert.Throws<InvalidOperationException>(() => db.Model);

            Assert.Contains("'Booking.Start' is a ZonedDateTime on the owned entity type 'Booking'", exception.Message);
        }

        [Fact]
        public void Owned_type_with_its_own_converter()
        {
            var pattern = ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFFFo<G> z", DateTimeZoneProviders.Tzdb);
            using var db = new AppointmentContext(b => b.Entity<Lesson>().OwnsOne(x => x.Booking, o => o.Property(x => x.Start).HasConversion(z => pattern.Format(z), s => pattern.Parse(s).Value)));

            Assert.Contains("[Booking_Start] nvarchar(max) NULL", db.Database.GenerateCreateScript());
        }

        /// <summary>The zone column's type from HasMaxLength, IsUnicode and IsFixedLength, as for a string.</summary>
        [Theory]
        [InlineData(32, false, false, "varchar(32)")]
        [InlineData(null, false, false, "varchar(50)")]
        [InlineData(40, true, true, "nchar(40)")]
        [InlineData(32, true, false, "nvarchar(32)")]
        public void Zone_column_facets(int? maxLength, bool unicode, bool fixedLength, string expectedType)
        {
            using var db = new AppointmentContext(b => b.Entity<Appointment>().ComplexProperty(x => x.When, c =>
            {
                var zone = c.Property(z => z.Zone).IsUnicode(unicode).IsFixedLength(fixedLength);
                if (maxLength != null)
                    zone.HasMaxLength(maxLength.Value);
            }));

            Assert.Contains($"[When_Zone] {expectedType} NOT NULL", db.Database.GenerateCreateScript());
        }

        public class Calendar
        {
            public int Id { get; set; }

            public ZonedDateTime this[int day]
            {
                get => default;
                set { }
            }
        }

        [Fact]
        public void Indexer_is_not_mapped()
        {
            using var db = new AppointmentContext(b => b.Entity<Calendar>());

            Assert.DoesNotContain("Item", db.Database.GenerateCreateScript());
        }

        public class ZonedDateTimeAsText() : ValueConverter<ZonedDateTime, string>(z => ToText(z), s => FromText(s))
        {
            private static readonly ZonedDateTimePattern _pattern
                = ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFFFo<G> z", DateTimeZoneProviders.Tzdb);

            private static string ToText(ZonedDateTime value) => _pattern.Format(value);

            private static ZonedDateTime FromText(string text) => _pattern.Parse(text).Value;
        }

        public class Meeting
        {
            public int Id { get; set; }

            public ZonedDateTime? Maybe { get; set; }

            public Slot Slot { get; set; }
        }

        public class Slot
        {
            public string Room { get; set; }

            public ZonedDateTime At { get; set; }
        }

        private static AppointmentContext MeetingContext(Action<ModelConfigurationBuilder> conventions)
            => new(b => b.Entity<Meeting>().ComplexProperty(x => x.Slot).IsRequired(), conventions);

        private static bool HasZonedDateTimeMapping(DbContext db)
            => db.Model.GetEntityTypes().Any(e => e.GetComplexProperties().Any(p => p.ComplexType.ClrType == typeof(ZonedDateTime)
                || p.ComplexType.GetComplexProperties().Any(n => n.ComplexType.ClrType == typeof(ZonedDateTime))));

        /// <summary>
        /// The same for every ZonedDateTime, with a value converter in ConfigureConventions: a common way to store them
        /// before this library mapped them. Nullable ones, and ones inside a complex type, too.
        /// </summary>
        [Fact]
        public void Own_converter_in_ConfigureConventions_takes_precedence()
        {
            using var db = MeetingContext(c => c.Properties<ZonedDateTime>().HaveConversion<ZonedDateTimeAsText>());

            var script = db.Database.GenerateCreateScript();

            Assert.Contains("[When] nvarchar(max) NOT NULL", script);
            Assert.Contains("[Maybe] nvarchar(max) NULL", script);
            Assert.Contains("[Slot_At] nvarchar(max) NOT NULL", script);
            Assert.DoesNotContain("_Zone", script);
            Assert.False(HasZonedDateTimeMapping(db));
            Assert.Contains("SELECT [a].[When]", db.Set<Appointment>().Select(x => x.When.ToInstant()).ToQueryString());
        }

        [Fact]
        public void IgnoreAny_in_ConfigureConventions_ignores_it()
        {
            using var db = MeetingContext(c => c.IgnoreAny<ZonedDateTime>());

            var script = db.Database.GenerateCreateScript();

            Assert.DoesNotContain("When", script);
            Assert.DoesNotContain("Maybe", script);
            Assert.DoesNotContain("Slot_At", script);
            Assert.Contains("[Slot_Room]", script);
            Assert.False(HasZonedDateTimeMapping(db));
        }

        [Fact]
        public void IgnoreAny_nullable_in_ConfigureConventions_ignores_nullable_ones()
        {
            using var db = MeetingContext(c => c.IgnoreAny<ZonedDateTime?>());

            Assert.DoesNotContain("Maybe", db.Database.GenerateCreateScript());
        }

        [Fact]
        public void Ignored_property_stays_ignored()
        {
            using var db = new AppointmentContext(b => b.Entity<Appointment>().Ignore(x => x.When));

            Assert.DoesNotContain("When", db.Database.GenerateCreateScript());
        }

        /// <summary>A migration creates the same columns, and the model snapshot records the complex property.</summary>
        [Fact]
        public void Migration()
        {
            using var db = new AppointmentContext(_ => { });
            var services = new ServiceCollection()
                .AddEntityFrameworkDesignTimeServices()
                .AddDbContextDesignTimeServices(db);
            var providerAssembly = typeof(SqlServerDbContextOptionsExtensions).Assembly;
            var providerServicesType = providerAssembly.GetType(providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName, throwOnError: true);
            ((IDesignTimeServices)Activator.CreateInstance(providerServicesType!)!).ConfigureDesignTimeServices(services);
            new SqlServerNodaTimeDesignTimeServices().ConfigureDesignTimeServices(services);
            using var serviceProvider = services.BuildServiceProvider();

            var migration = serviceProvider.GetRequiredService<IMigrationsScaffolder>().ScaffoldMigration("Initial", "Tests");

            Assert.Contains("When = table.Column<DateTimeOffset>(type: \"datetimeoffset\", nullable: false)", migration.MigrationCode);
            Assert.Contains("When_Zone = table.Column<string>(type: \"nvarchar(50)\", nullable: false)", migration.MigrationCode);
            Assert.Contains("ComplexProperty(typeof(Dictionary<string, object>), \"When\"", migration.SnapshotCode);
        }

        /// <summary>
        /// A ZonedDateTime is mapped through its private fields, so a NodaTime version without them would break the mapping.
        /// This fails first, with a clear reason.
        /// </summary>
        [Fact]
        public void NodaTime_has_the_fields_the_mapping_uses()
        {
            Assert.Equal(typeof(OffsetDateTime), typeof(ZonedDateTime).GetField("offsetDateTime", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
            Assert.Equal(typeof(DateTimeZone), typeof(ZonedDateTime).GetField("zone", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
        }
    }
}
