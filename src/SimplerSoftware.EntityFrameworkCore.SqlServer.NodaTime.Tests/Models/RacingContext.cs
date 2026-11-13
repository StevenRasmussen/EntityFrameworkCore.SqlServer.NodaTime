using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;

using NodaTime;

using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Logging;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests.Models
{
    public class RacingContext : DbContext
    {
        public RacingContext(DbContextOptions options)
            : base(options)
        {
        }

        private readonly TestLoggerFactory _loggerFactory = new TestLoggerFactory();

        public DbSet<SupportedNodaTypes> TypeResults { get; set; }

        public DbSet<PrecisionNodaTypes> PrecisionTypes { get; set; }

        public DbSet<Race> Race { get; set; }

        public DbSet<RaceResult> RaceResult { get; set; }

        public DbSet<RaceSplit> RaceSplit { get; set; }

        public DbSet<NodaTimeCollections> NodaTimeCollections { get; set; }

        public DbSet<DurationStorageTypes> DurationStorageTypes { get; set; }

        public DbSet<ZonedEvent> ZonedEvents { get; set; }

        public DbSet<PeriodEvent> PeriodEvents { get; set; }

        public string Sql => _loggerFactory.Logger.Sql;

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseLoggerFactory(_loggerFactory);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureMappings(modelBuilder);
            SeedData(modelBuilder);
        }

        /// <summary>The column configuration without the seed data. CompiledModelTests builds its compiled model from this too.</summary>
        public static void ConfigureMappings(ModelBuilder modelBuilder)
        {
            // EF Core maps the list properties to JSON arrays by convention; DurationsInNanoseconds stores its elements
            // as bigint (#38). No seed data here: the tests insert their own rows inside a transaction that is rolled back.
            modelBuilder.Entity<NodaTimeCollections>(entity =>
            {
                entity.PrimitiveCollection(x => x.DurationsInNanoseconds).ElementType().HasStoreType("bigint");
                entity.ComplexProperty(x => x.Json, b =>
                {
                    b.ToJson();
                    b.Property(x => x.DurationAsBigInt).HasColumnType("bigint");
                    b.Property(x => x.DurationAsDateTime2).HasColumnType("datetime2");
                    b.Property(x => x.DurationAsText).HasColumnType("nvarchar(28)");
                });
            });

            modelBuilder.Entity<PrecisionNodaTypes>(entity =>
            {
                entity.Property(x => x.InstantAsDateTime).HasColumnType("datetime");
                entity.Property(x => x.InstantAsSmallDateTime).HasColumnType("smalldatetime");
                entity.Property(x => x.InstantAsDateTime2Of0).HasColumnType("datetime2(0)");
                entity.Property(x => x.InstantWithPrecision3).HasPrecision(3);

                entity.Property(x => x.LocalDateTimeAsDateTime).HasColumnType("datetime");
                entity.Property(x => x.LocalDateTimeAsSmallDateTime).HasColumnType("smalldatetime");
                entity.Property(x => x.LocalDateTimeAsDateTime2Of0).HasColumnType("datetime2(0)");
                entity.Property(x => x.LocalDateTimeWithPrecision3).HasPrecision(3);

                entity.Property(x => x.OffsetDateTimeAsDateTimeOffsetOf3).HasColumnType("datetimeoffset(3)");
                entity.Property(x => x.OffsetDateTimeWithPrecision2).HasPrecision(2);

                entity.Property(x => x.LocalTimeAsTimeOf3).HasColumnType("time(3)");
                entity.Property(x => x.LocalTimeWithPrecision2).HasPrecision(2);
                entity.Property(x => x.DurationAsTimeOf3).HasColumnType("time(3)");
                entity.Property(x => x.DurationWithPrecision2).HasPrecision(2);
            });

            // The ZonedDateTime properties need no configuration (#22); Schedule is the application's own complex type.
            modelBuilder.Entity<ZonedEvent>().ComplexProperty(x => x.Schedule).IsRequired();
            // A fixed-length zone column, which pads the ID with spaces.
            modelBuilder.Entity<ZonedEvent>().ComplexProperty(x => x.Start, b => b.Property(z => z.Zone).HasColumnType("char(32)"));

            modelBuilder.Entity<PeriodEvent>().ComplexProperty(x => x.Every).IsRequired();
            modelBuilder.Entity<PeriodEvent>().ComplexProperty(x => x.Rule).IsRequired();

            modelBuilder.Entity<DurationStorageTypes>(entity =>
            {
                entity.Property(x => x.BigInt).HasColumnType("bigint");
                entity.Property(x => x.Int).HasColumnType("int");
                entity.Property(x => x.DateTime2).HasColumnType("datetime2");
                entity.Property(x => x.Text).HasColumnType("nvarchar(28)");
            });
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SupportedNodaTypes>()
                .HasData(
                    CreateSupportedNodaTypes(1, withNullables: true),
                    CreateSupportedNodaTypes(2, withNullables: false));

            modelBuilder.Entity<PrecisionNodaTypes>()
                .HasData(
                    CreatePrecisionNodaTypes(1, new LocalDateTime(2019, 1, 1, 8, 0)),
                    CreatePrecisionNodaTypes(2, new LocalDateTime(2019, 7, 1, 14, 30)));

            modelBuilder.Entity<DurationStorageTypes>().HasData(DurationStorageRows);

            modelBuilder.Entity<Race>()
                .HasData(
                    new Race { Id = 1, Date = new LocalDate(2019, 1, 1), DateTimeDate = new DateTime(2019, 1, 1), ScheduledStartTime = new LocalTime(8, 0, 0, 0), ScheduledDuration = DurationExtensions.FromParts(1, 0, 0, 0), ScheduledStart = new LocalDateTime(2019, 1, 1, 8, 0, 0, 0) },
                    new Race { Id = 2, Date = new LocalDate(2019, 2, 1), DateTimeDate = new DateTime(2019, 2, 1), ScheduledStartTime = new LocalTime(9, 1, 1, 1), ScheduledDuration = DurationExtensions.FromParts(2, 1, 1, 1), ScheduledStart = new LocalDateTime(2019, 2, 1, 9, 1, 1, 1) },
                    new Race { Id = 3, Date = new LocalDate(2019, 3, 1), DateTimeDate = new DateTime(2019, 3, 1), ScheduledStartTime = new LocalTime(10, 2, 2, 2), ScheduledDuration = DurationExtensions.FromParts(3, 2, 2, 2), ScheduledStart = new LocalDateTime(2019, 3, 1, 10, 2, 2, 2) },
                    new Race { Id = 4, Date = new LocalDate(2019, 4, 1), DateTimeDate = new DateTime(2019, 4, 1), ScheduledStartTime = new LocalTime(11, 3, 3, 3), ScheduledDuration = DurationExtensions.FromParts(4, 3, 3, 3), ScheduledStart = new LocalDateTime(2019, 4, 1, 11, 3, 3, 3) },
                    new Race { Id = 5, Date = new LocalDate(2019, 5, 1), DateTimeDate = new DateTime(2019, 5, 1), ScheduledStartTime = new LocalTime(12, 4, 4, 4), ScheduledDuration = DurationExtensions.FromParts(5, 4, 4, 4), ScheduledStart = new LocalDateTime(2019, 5, 1, 12, 4, 4, 4) },
                    new Race { Id = 6, Date = new LocalDate(2019, 6, 1), DateTimeDate = new DateTime(2019, 6, 1), ScheduledStartTime = new LocalTime(13, 5, 5, 5), ScheduledDuration = DurationExtensions.FromParts(6, 5, 5, 5), ScheduledStart = new LocalDateTime(2019, 6, 1, 13, 5, 5, 5) },
                    new Race { Id = 7, Date = new LocalDate(2019, 7, 1), DateTimeDate = new DateTime(2019, 7, 1), ScheduledStartTime = new LocalTime(14, 6, 6, 6), ScheduledDuration = DurationExtensions.FromParts(7, 6, 6, 6), ScheduledStart = new LocalDateTime(2019, 7, 1, 14, 6, 6, 6) },
                    new Race { Id = 8, Date = new LocalDate(2019, 8, 1), DateTimeDate = new DateTime(2019, 8, 1), ScheduledStartTime = new LocalTime(15, 7, 7, 7), ScheduledDuration = DurationExtensions.FromParts(8, 7, 7, 7), ScheduledStart = new LocalDateTime(2019, 8, 1, 15, 7, 7, 7) },
                    new Race { Id = 9, Date = new LocalDate(2019, 9, 1), DateTimeDate = new DateTime(2019, 9, 1), ScheduledStartTime = new LocalTime(16, 8, 8, 8), ScheduledDuration = DurationExtensions.FromParts(9, 8, 8, 8), ScheduledStart = new LocalDateTime(2019, 9, 1, 16, 8, 8, 8) },
                    new Race { Id = 10, Date = new LocalDate(2019, 10, 1), DateTimeDate = new DateTime(2019, 10, 1), ScheduledStartTime = new LocalTime(17, 9, 9, 9), ScheduledDuration = DurationExtensions.FromParts(10, 9, 9, 9), ScheduledStart = new LocalDateTime(2019, 10, 1, 17, 9, 9, 9) },
                    new Race { Id = 11, Date = new LocalDate(2019, 11, 1), DateTimeDate = new DateTime(2019, 11, 1), ScheduledStartTime = new LocalTime(18, 10, 10, 10), ScheduledDuration = DurationExtensions.FromParts(11, 10, 10, 10), ScheduledStart = new LocalDateTime(2019, 11, 1, 18, 10, 10, 10) },
                    new Race { Id = 12, Date = new LocalDate(2019, 12, 1), DateTimeDate = new DateTime(2019, 12, 1), ScheduledStartTime = new LocalTime(19, 11, 11, 11), ScheduledDuration = DurationExtensions.FromParts(12, 11, 11, 11), ScheduledStart = new LocalDateTime(2019, 12, 1, 19, 11, 11, 11) }
                );


            modelBuilder.Entity<RaceResult>()
                .HasData(
                    new RaceResult { Id = 1, StartTime = Instant.FromUtc(2019, 1, 1, 8, 0), EndTime = Instant.FromUtc(2019, 1, 1, 9, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 1, 1, 8, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 0, 0, 0) },
                    new RaceResult { Id = 2, StartTime = Instant.FromUtc(2019, 2, 1, 9, 0), EndTime = Instant.FromUtc(2019, 2, 1, 10, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 2, 1, 9, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 1, 0, 0) },
                    new RaceResult { Id = 3, StartTime = Instant.FromUtc(2019, 3, 1, 10, 0), EndTime = Instant.FromUtc(2019, 3, 1, 11, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 3, 1, 10, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 2, 0, 0) },
                    new RaceResult { Id = 4, StartTime = Instant.FromUtc(2019, 4, 1, 11, 0), EndTime = Instant.FromUtc(2019, 4, 1, 12, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 4, 1, 11, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 3, 0, 0) },
                    new RaceResult { Id = 5, StartTime = Instant.FromUtc(2019, 5, 1, 12, 0), EndTime = Instant.FromUtc(2019, 5, 1, 13, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 5, 1, 12, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 4, 0, 0) },
                    new RaceResult { Id = 6, StartTime = Instant.FromUtc(2019, 6, 1, 13, 0), EndTime = Instant.FromUtc(2019, 6, 1, 14, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 6, 1, 13, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 5, 0, 0) },
                    new RaceResult { Id = 7, StartTime = Instant.FromUtc(2019, 7, 1, 14, 0), EndTime = Instant.FromUtc(2019, 7, 1, 15, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 7, 1, 14, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 6, 0, 0) },
                    new RaceResult { Id = 8, StartTime = Instant.FromUtc(2019, 8, 1, 15, 0), EndTime = Instant.FromUtc(2019, 8, 1, 16, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 8, 1, 15, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 7, 0, 0) },
                    new RaceResult { Id = 9, StartTime = Instant.FromUtc(2019, 9, 1, 16, 0), EndTime = Instant.FromUtc(2019, 9, 1, 17, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 9, 1, 16, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 8, 0, 0) },
                    new RaceResult { Id = 10, StartTime = Instant.FromUtc(2019, 10, 1, 17, 0), EndTime = Instant.FromUtc(2019, 10, 1, 18, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 10, 1, 17, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 9, 0, 0) },
                    new RaceResult { Id = 11, StartTime = Instant.FromUtc(2019, 11, 1, 18, 0), EndTime = Instant.FromUtc(2019, 11, 1, 19, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 11, 1, 18, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 10, 0, 0) },
                    new RaceResult { Id = 12, StartTime = Instant.FromUtc(2019, 12, 1, 19, 0), EndTime = Instant.FromUtc(2019, 12, 1, 20, 0), StartTimeOffset = OffsetDateTime.FromDateTimeOffset(new DateTimeOffset(new DateTime(2019, 12, 1, 19, 0, 0), TimeSpan.FromHours(5))), OffsetFromWinner = DurationExtensions.FromParts(0, 0, 0, 11, 0, 0) });

            modelBuilder.Entity<RaceSplit>()
                .HasData(
                    new RaceSplit { Id = 1, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 1), TimeStampLocalTime = LocalTimeExtensions.FromParts(1, 1, 1, 1, 1, 100), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 1, 1, 1, 1, 1, 100, 300) },
                    new RaceSplit { Id = 2, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 2, 2, 2, 2, 2, 2), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 2, 2, 2, 2, 2, 2), TimeStampLocalTime = LocalTimeExtensions.FromParts(2, 2, 2, 2, 1, 100), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 2, 2, 2, 2, 2, 200, 300) },
                    new RaceSplit { Id = 3, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 3, 3, 3, 3, 3, 3), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 3, 3, 3, 3, 3, 3), TimeStampLocalTime = LocalTimeExtensions.FromParts(3, 3, 3, 3, 3, 300), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 3, 3, 3, 3, 3, 300, 300) },
                    new RaceSplit { Id = 4, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 4, 4, 4, 4, 4, 4), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 4, 4, 4, 4, 4, 4), TimeStampLocalTime = LocalTimeExtensions.FromParts(4, 4, 4, 4, 4, 400), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 4, 4, 4, 4, 4, 400, 300) },
                    new RaceSplit { Id = 5, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 5, 5, 5, 5, 5, 5), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 5, 5, 5, 5, 5, 5), TimeStampLocalTime = LocalTimeExtensions.FromParts(5, 5, 5, 5, 5, 500), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 5, 5, 5, 5, 5, 500, 300) },
                    new RaceSplit { Id = 6, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 6, 6, 6, 6, 6, 6), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 6, 6, 6, 6, 6, 6), TimeStampLocalTime = LocalTimeExtensions.FromParts(6, 6, 6, 6, 6, 600), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 6, 6, 6, 6, 6, 600, 300) },
                    new RaceSplit { Id = 7, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 7, 7, 7, 7, 7, 7), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 7, 7, 7, 7, 7, 7), TimeStampLocalTime = LocalTimeExtensions.FromParts(7, 7, 7, 7, 7, 700), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 7, 7, 7, 7, 7, 700, 300) },
                    new RaceSplit { Id = 8, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 8, 8, 8, 8, 8, 8), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 8, 8, 8, 8, 8, 8), TimeStampLocalTime = LocalTimeExtensions.FromParts(8, 8, 8, 8, 8, 800), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 8, 8, 8, 8, 8, 800, 300) },
                    new RaceSplit { Id = 9, TimeStampInstant = InstantExtensions.FromParts(2020, 1, 1, 9, 9, 9, 9, 9, 9), TimeStampLocalDateTime = LocalDateTimeExtensions.FromParts(2020, 1, 1, 9, 9, 9, 9, 9, 9), TimeStampLocalTime = LocalTimeExtensions.FromParts(9, 9, 9, 9, 9, 900), TimeStampOffsetDateTime = OffsetDateTimeExtensions.FromParts(2020, 1, 1, 9, 9, 9, 9, 9, 900, 300) }
                );
        }

        private static readonly Duration WithNanoseconds = Duration.FromHours(1) + Duration.FromMinutes(30) + Duration.FromSeconds(15) + Duration.FromNanoseconds(123_456_789);
        public static readonly Duration NegativeWithFraction = -(Duration.FromHours(26) + Duration.FromMinutes(3) + Duration.FromMilliseconds(500));

        /// <summary>
        /// Values as written; what comes back is truncated to 100ns for time and datetime2, and to whole seconds for
        /// int, which is why the tests compare query results with .NET's answer for the values read back.
        /// </summary>
        private static readonly DateTimeZone Paris = DateTimeZoneProviders.Tzdb["Europe/Paris"];
        private static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];
        private static readonly DateTimeZone Kathmandu = DateTimeZoneProviders.Tzdb["Asia/Kathmandu"];

        /// <summary>
        /// Summer and winter in Paris, an ambiguous New York time (1:30 on the day the clocks go back, here the earlier
        /// one, in EDT), Kathmandu's +05:45, and a time with 100ns digits (all a datetimeoffset keeps). EF Core can't seed
        /// complex properties with HasData, so DatabaseTestFixture inserts these, as ids 1 to 4.
        /// </summary>
        /// <summary>Inserted by the test fixture: HasData can't seed a complex type.</summary>
        public static PeriodEvent[] PeriodEventRows() =>
        [
            // Years, then months: 2024-02-29 plus a year is 2025-02-28, then plus a month, 2025-03-28.
            new()
            {
                Every = Period.FromYears(1) + Period.FromMonths(1), Shift = Period.FromHours(8),
                From = new LocalDateTime(2024, 2, 29, 23, 30), Until = new LocalDateTime(2025, 3, 28, 23, 29, 59),
                FromDate = new LocalDate(2024, 2, 29), UntilDate = new LocalDate(2025, 3, 28),
                At = new LocalTime(23, 30), UntilTime = new LocalTime(1, 0),
                Rule = new() { Name = "yearly", Every = Period.FromYears(1) },
            },
            // Odd units, kept as they are: a day, minus an hour, 90 minutes, 3 ticks and 200ns. Ends before it starts.
            new()
            {
                Every = new PeriodBuilder { Days = 1, Hours = -1, Minutes = 90, Ticks = 3, Nanoseconds = 200 }.Build(),
                Grace = Period.FromWeeks(2), Shift = Period.FromMinutes(-90),
                From = new LocalDateTime(2024, 1, 31, 10, 0), Until = new LocalDateTime(2023, 12, 31, 9, 0),
                FromDate = new LocalDate(2024, 1, 31), UntilDate = new LocalDate(2023, 1, 31),
                At = new LocalTime(10, 0), UntilTime = new LocalTime(9, 0),
                Rule = new() { Name = "odd", Every = new PeriodBuilder { Hours = 25, Seconds = -1 }.Build() },
            },
            // Time units only, more than a day of them.
            new()
            {
                Every = Period.FromHours(36), Grace = Period.FromDays(3), Shift = new PeriodBuilder { Hours = 25, Minutes = 30 }.Build(),
                From = new LocalDateTime(2024, 12, 31, 23, 0), Until = new LocalDateTime(2026, 1, 1, 0, 0),
                FromDate = new LocalDate(2024, 12, 31), UntilDate = new LocalDate(2024, 3, 1),
                At = new LocalTime(23, 0), UntilTime = new LocalTime(23, 0, 0, 1),
                Rule = new() { Name = "none" },
            },
            // Negative units, and a zero period.
            new()
            {
                Every = new PeriodBuilder { Months = -1, Days = -5 }.Build(), Grace = Period.Zero,
                From = new LocalDateTime(2024, 3, 31, 0, 0), Until = new LocalDateTime(2024, 3, 31, 0, 0),
                FromDate = new LocalDate(2024, 3, 31), UntilDate = new LocalDate(2020, 2, 29),
                At = new LocalTime(0, 0), UntilTime = new LocalTime(0, 0).PlusNanoseconds(100),
                Rule = new() { Name = "zero", Every = Period.Zero },
            },
        ];

        public static ZonedEvent[] ZonedEventRows() =>
        [
            new() { Start = new LocalDateTime(2024, 7, 1, 10, 0).InZoneStrictly(Paris), End = new LocalDateTime(2024, 7, 1, 12, 30).InZoneStrictly(Paris), HomeZone = Paris, UtcOffset = Offset.FromHours(2), Booked = new OffsetDateTime(new LocalDateTime(2024, 6, 1, 9, 0), Offset.FromHours(2)) },
            new() { Start = new LocalDateTime(2024, 1, 15, 10, 0, 0, 123).PlusNanoseconds(4500).InZoneStrictly(Paris), HomeZone = Paris, Booked = new OffsetDateTime(new LocalDateTime(2024, 1, 1, 23, 0), Offset.FromHours(-5)) },
            new() { Start = new LocalDateTime(2024, 11, 3, 1, 30).InZone(NewYork, global::NodaTime.TimeZones.Resolvers.LenientResolver), End = new LocalDateTime(2024, 11, 3, 1, 30).InZone(NewYork, global::NodaTime.TimeZones.Resolvers.LenientResolver).PlusHours(1), UtcOffset = Offset.FromHours(-4), Booked = new OffsetDateTime(new LocalDateTime(2024, 11, 3, 5, 0), Offset.Zero), Schedule = new() { Reminder = new LocalDateTime(2024, 11, 2, 9, 0).InZoneStrictly(NewYork) } },
            new() { Start = new LocalDateTime(2024, 3, 1, 23, 59, 59).InZoneStrictly(Kathmandu), HomeZone = Kathmandu, UtcOffset = Offset.FromHoursAndMinutes(5, 45), Booked = new OffsetDateTime(new LocalDateTime(2024, 3, 1, 18, 14, 59), Offset.Zero), Schedule = new() { Reminder = new LocalDateTime(2024, 2, 1, 8, 0).InZoneStrictly(Paris) } },
        ];

        public static readonly DurationStorageTypes[] DurationStorageRows =
        [
            new() { Id = 1, Time = Duration.Zero, BigInt = Duration.Zero, Int = Duration.Zero, DateTime2 = Duration.Zero, Text = Duration.Zero },
            new() { Id = 2, Time = Duration.FromHours(1), BigInt = Duration.FromHours(1), Int = Duration.FromHours(1), DateTime2 = Duration.FromHours(1), Text = Duration.FromHours(1) },
            new() { Id = 3, Time = WithNanoseconds, BigInt = WithNanoseconds, Int = WithNanoseconds, DateTime2 = WithNanoseconds, Text = WithNanoseconds },
            new() { Id = 4, Time = Duration.FromDays(1) - Duration.FromNanoseconds(100), BigInt = Duration.FromDays(1) - Duration.Epsilon, Int = Duration.FromDays(1) - Duration.Epsilon, DateTime2 = Duration.FromDays(1) - Duration.Epsilon, Text = Duration.FromDays(1) - Duration.Epsilon },
            new() { Id = 5 },
            new() { Id = 6, Time = Duration.FromHours(12), BigInt = Duration.FromHours(25), Int = Duration.FromHours(25), DateTime2 = Duration.FromHours(25), Text = Duration.FromHours(25) },
            new() { Id = 7, BigInt = NegativeWithFraction, Int = NegativeWithFraction, DateTime2 = NegativeWithFraction, Text = NegativeWithFraction },
            new() { Id = 8, Time = Duration.FromHours(23), BigInt = Duration.FromDays(100) + Duration.Epsilon, Int = Duration.FromDays(100), DateTime2 = Duration.FromDays(100) + Duration.Epsilon, Text = Duration.FromDays(100) + Duration.Epsilon },
            new() { Id = 9, BigInt = -Duration.FromMilliseconds(1500), Int = -Duration.FromMilliseconds(1500), DateTime2 = -Duration.FromMilliseconds(1500), Text = -Duration.FromMilliseconds(1500) },
            new() { Id = 10, Time = Duration.FromHours(6), BigInt = Duration.FromDays(100 * 365), DateTime2 = Duration.FromDays(100 * 365), Text = Duration.FromDays(100 * 365) },
            new() { Id = 11, Time = Duration.FromMinutes(30), BigInt = -Duration.FromMinutes(30), Int = Duration.FromMinutes(30), DateTime2 = -Duration.FromMinutes(30), Text = -Duration.FromMinutes(30) },
        ];

        /// <summary>Row 1 has every nullable property populated; row 2 has every nullable property null.</summary>
        private static SupportedNodaTypes CreateSupportedNodaTypes(int id, bool withNullables) => new SupportedNodaTypes
        {
            Id = id,
            DurationNonNull = Duration.FromHours(1),
            DurationNull = withNullables ? Duration.FromMinutes(90) : null,
            InstantNonNull = Instant.FromUtc(2019, 7, 1, 1, 0),
            InstantNull = withNullables ? Instant.FromUtc(2020, 2, 29, 12, 30) : null,
            LocalDateNonNull = new LocalDate(2019, 7, 1),
            LocalDateNull = withNullables ? new LocalDate(2000, 2, 29) : null,
            LocalDateTimeNonNull = new LocalDateTime(2019, 7, 1, 1, 0),
            LocalDateTimeNull = withNullables ? new LocalDateTime(2020, 2, 29, 12, 30) : null,
            LocalTimeNonNull = new LocalTime(1, 0),
            LocalTimeNull = withNullables ? new LocalTime(12, 30) : null,
            OffsetDateTimeNonNull = new OffsetDateTime(new LocalDateTime(2019, 7, 1, 1, 0), Offset.Zero),
            OffsetDateTimeNull = withNullables ? new OffsetDateTime(new LocalDateTime(2020, 2, 29, 12, 30), Offset.FromHoursAndMinutes(5, 30)) : null,
        };

        private static PrecisionNodaTypes CreatePrecisionNodaTypes(int id, LocalDateTime value) => new PrecisionNodaTypes
        {
            Id = id,
            InstantAsDateTime = value.InUtc().ToInstant(),
            InstantAsSmallDateTime = value.InUtc().ToInstant(),
            InstantAsDateTime2Of0 = value.InUtc().ToInstant(),
            InstantWithPrecision3 = value.InUtc().ToInstant(),
            LocalDateTimeAsDateTime = value,
            LocalDateTimeAsSmallDateTime = value,
            LocalDateTimeAsDateTime2Of0 = value,
            LocalDateTimeWithPrecision3 = value,
            OffsetDateTimeAsDateTimeOffsetOf3 = new OffsetDateTime(value, Offset.FromHoursAndMinutes(5, 30)),
            OffsetDateTimeWithPrecision2 = new OffsetDateTime(value, Offset.FromHoursAndMinutes(5, 30)),
            LocalTimeAsTimeOf3 = value.TimeOfDay,
            LocalTimeWithPrecision2 = value.TimeOfDay,
            DurationAsTimeOf3 = Duration.FromTicks(value.TimeOfDay.TickOfDay),
            DurationWithPrecision2 = Duration.FromTicks(value.TimeOfDay.TickOfDay),
        };
    }
}
