using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>
    /// 'dotnet ef dbcontext scaffold' gives date and time columns NodaTime types, and the context it generates reads them.
    /// </summary>
    public class ScaffoldingTests : QueryTestBase
    {
        public ScaffoldingTests(DatabaseTestFixture databaseTestFixture)
            : base(databaseTestFixture)
        {
        }

        private static readonly (string Name, string StoreType, string Literal, object Value)[] Columns =
        [
            ("DateTime2", "datetime2", "'2019-02-01T10:01:01.1234567'", Instant.FromUtc(2019, 2, 1, 10, 1, 1).PlusTicks(1234567)),
            ("DateTime2Precision", "datetime2(3)", "'2019-02-01T10:01:01.123'", Instant.FromUtc(2019, 2, 1, 10, 1, 1).PlusTicks(1230000)),
            ("DateTime", "datetime", "'2019-02-01T10:01:01'", Instant.FromUtc(2019, 2, 1, 10, 1, 1)),
            ("SmallDateTime", "smalldatetime", "'2019-02-01T10:01:00'", Instant.FromUtc(2019, 2, 1, 10, 1)),
            ("DateTimeOffset", "datetimeoffset", "'2019-02-01T10:01:01.1234567+05:30'", new LocalDateTime(2019, 2, 1, 10, 1, 1).PlusTicks(1234567).WithOffset(Offset.FromHoursAndMinutes(5, 30))),
            ("DateTimeOffsetPrecision", "datetimeoffset(3)", "'2019-02-01T10:01:01.123+05:30'", new LocalDateTime(2019, 2, 1, 10, 1, 1, 123).WithOffset(Offset.FromHoursAndMinutes(5, 30))),
            ("Date", "date", "'2019-02-01'", new LocalDate(2019, 2, 1)),
            ("Time", "time", "'10:01:01.1234567'", new LocalTime(10, 1, 1).PlusTicks(1234567)),
            ("TimePrecision", "time(3)", "'10:01:01.123'", new LocalTime(10, 1, 1, 123)),
            // A Duration, Offset or DateTimeZone can be stored in these too, but most such columns hold plain numbers and text.
            ("Int", "int", "42", 42),
            ("BigInt", "bigint", "42", 42L),
            ("Text", "nvarchar(50)", "N'Europe/Paris'", "Europe/Paris"),
        ];

        /// <summary>The provider's design-time services, found the way 'dotnet ef' finds them, then this library's.</summary>
        private static ServiceProvider BuildDesignTimeServices(IServiceCollection services)
        {
            var providerAssembly = typeof(SqlServerDbContextOptionsExtensions).Assembly;
            var providerServicesType = providerAssembly.GetType(providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!.TypeName, throwOnError: true);
            ((IDesignTimeServices)Activator.CreateInstance(providerServicesType!)!).ConfigureDesignTimeServices(services);
            new SqlServerNodaTimeDesignTimeServices().ConfigureDesignTimeServices(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Date_and_time_columns_are_scaffolded_as_NodaTime_types()
        {
            var createTable = $"""
                DROP TABLE IF EXISTS [Scaffolding];
                CREATE TABLE [Scaffolding] ([Id] int PRIMARY KEY, {string.Join(", ", Columns.Select(c => $"[{c.Name}] {c.StoreType} NOT NULL"))});
                INSERT INTO [Scaffolding] VALUES (1, {string.Join(", ", Columns.Select(c => c.Literal))});
                """;
            this.Db.Database.ExecuteSqlRaw(createTable);
            try
            {
                // The services 'dotnet ef dbcontext scaffold' uses, without a DbContext.
                using var serviceProvider = BuildDesignTimeServices(new ServiceCollection().AddEntityFrameworkDesignTimeServices());
                var scaffolded = serviceProvider.GetRequiredService<IReverseEngineerScaffolder>().ScaffoldModel(
                    this.SqlConnection.ConnectionString,
                    new DatabaseModelFactoryOptions(tables: ["Scaffolding"]),
                    new ModelReverseEngineerOptions(),
                    new ModelCodeGenerationOptions
                    {
                        ContextName = "ScaffoldedContext",
                        ContextNamespace = "ScaffoldingTestsGenerated",
                        ModelNamespace = "ScaffoldingTestsGenerated",
                        Language = "C#",
                    });
                var assembly = GeneratedCode.Compile("ScaffoldingTestsGenerated", scaffolded.AdditionalFiles.Prepend(scaffolded.ContextFile));

                // The generated OnConfiguring calls UseNodaTime(): without it, the NodaTime properties couldn't be mapped.
                using var context = (DbContext)Activator.CreateInstance(assembly.GetType("ScaffoldingTestsGenerated.ScaffoldedContext", throwOnError: true)!)!;
                var entityType = context.Model.GetEntityTypes().Single();
                var row = context.Find(entityType.ClrType, 1);

                Assert.All(Columns, column =>
                {
                    var property = entityType.FindProperty(column.Name)!;
                    Assert.Equal(column.Value.GetType(), property.ClrType);
                    Assert.Equal(column.StoreType, property.GetColumnType());
                    Assert.Equal(column.Value, property.PropertyInfo!.GetValue(row));
                });
            }
            finally
            {
                this.Db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS [Scaffolding]");
            }
        }

        /// <summary>
        /// Lookups by store type alone give EF Core's types where there's a DbContext, as for 'migrations add' and
        /// 'dbcontext optimize', which can precompile queries: EF Core uses the context's type mapping source there, not
        /// one with the scaffolding plugin. Queries need EF Core's types for expressions like DateTime.UtcNow (#52).
        /// </summary>
        [Fact]
        public void Design_time_services_with_a_DbContext_keep_EF_Core_types_for_store_type_lookups()
        {
            using var serviceProvider = BuildDesignTimeServices(new ServiceCollection().AddEntityFrameworkDesignTimeServices().AddDbContextDesignTimeServices(this.Db));
            var typeMappingSource = serviceProvider.GetRequiredService<IRelationalTypeMappingSource>();

            Assert.Equal(typeof(DateTime), typeMappingSource.FindMapping("datetime2")!.ClrType);
            Assert.Equal(typeof(DateOnly), typeMappingSource.FindMapping("date")!.ClrType);
            Assert.Equal(typeof(TimeOnly), typeMappingSource.FindMapping("time")!.ClrType);
            Assert.Equal(typeof(DateTimeOffset), typeMappingSource.FindMapping("datetimeoffset")!.ClrType);
        }
    }
}
