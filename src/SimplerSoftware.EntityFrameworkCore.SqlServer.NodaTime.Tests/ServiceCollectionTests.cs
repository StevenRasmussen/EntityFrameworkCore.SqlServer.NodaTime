using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    public class ServiceCollectionTests
    {
        [Fact]
        public void CustomServiceCollection_ShouldNotThrowError()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .AddNodaTime()
                .BuildServiceProvider(true);

            var builder = new DbContextOptionsBuilder<SimpleContext>()
                .UseInternalServiceProvider(serviceProvider)
                .UseSqlServer("Does not matter since we won't try to connect to the DB", opt => { opt.UseNodaTime(); });

            using var dbContext = new SimpleContext(builder.Options);

            dbContext.Add(new SupportedNodaTypes());
            Assert.Single(dbContext.ChangeTracker.Entries());
        }

        /// <summary>An internal service provider without NodaTime's services can't work, so UseNodaTime says what's missing.</summary>
        [Fact]
        public void Internal_service_provider_without_AddNodaTime()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .BuildServiceProvider(true);

            var builder = new DbContextOptionsBuilder<SimpleContext>()
                .UseInternalServiceProvider(serviceProvider)
                .UseSqlServer("Does not matter since we won't try to connect to the DB", opt => { opt.UseNodaTime(); });

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                using var dbContext = new SimpleContext(builder.Options);
                return dbContext.Model;
            });
            Assert.Equal("UseNodaTime requires AddNodaTime to be called on the internal service provider used.", exception.Message);
        }

        private class SimpleContext : DbContext
        {
            public SimpleContext(DbContextOptions options)
                : base(options)
            {
            }

            public DbSet<SupportedNodaTypes> TypeResults { get; set; }
        }
    }
}