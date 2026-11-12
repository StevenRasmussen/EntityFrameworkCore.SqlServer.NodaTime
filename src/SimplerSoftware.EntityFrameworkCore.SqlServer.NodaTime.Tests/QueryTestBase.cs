using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models;
using System;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    [Collection(nameof(DatabaseTestCollection))]
    public abstract class QueryTestBase
    {
        public QueryTestBase(DatabaseTestFixture databaseTestFixture)
        {
            this.Db = databaseTestFixture.DbContext;
            this.DbContextOptions = databaseTestFixture.DbContextOptions;
            this.SqlConnection = databaseTestFixture.SqlConnection;
        }

        protected RacingContext Db { get; }

        protected DbContextOptions<RacingContext> DbContextOptions { get; }

        protected DbFunctions Functions { get; }

        protected string RaceSelectStatement => "SELECT [r].[Id], [r].[Date], [r].[DateTimeDate], [r].[ScheduledDuration], [r].[ScheduledStart], [r].[ScheduledStartTime] FROM [Race] AS [r]";

        protected string RaceResultSelectStatement => "SELECT [r].[Id], [r].[EndTime], [r].[OffsetFromWinner], [r].[StartTime], [r].[StartTimeOffset] FROM [RaceResult] AS [r]";

        protected string RaceSplitSelectStatement => "SELECT [r].[Id], [r].[TimeStampInstant], [r].[TimeStampLocalDateTime], [r].[TimeStampLocalTime], [r].[TimeStampOffsetDateTime] FROM [RaceSplit] AS [r]";

        protected SqlConnection SqlConnection { get; }

        protected static string condense(string str)
        {
            var split = str.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", split);
        }

        /// <summary>
        /// Formats a NodaTime value losslessly (NodaTime's ToString() drops fractional seconds), so that
        /// Assert.Equal(Iso(expected), Iso(actual)) both compares exactly and produces a readable failure message.
        /// </summary>
        protected static string Iso(object value) => value switch
        {
            null => "null",
            Instant v => InstantPattern.ExtendedIso.Format(v),
            LocalDateTime v => LocalDateTimePattern.ExtendedIso.Format(v),
            OffsetDateTime v => OffsetDateTimePattern.ExtendedIso.Format(v),
            LocalDate v => LocalDatePattern.Iso.Format(v),
            LocalTime v => LocalTimePattern.ExtendedIso.Format(v),
            Duration v => DurationPattern.Roundtrip.Format(v),
            _ => throw new ArgumentException($"Unsupported type {value.GetType()}", nameof(value)),
        };
    }
}
