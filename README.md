![.NET Core](https://github.com/StevenRasmussen/EFCore.SqlServer.NodaTime/workflows/.NET%20Core/badge.svg?branch=master)
[![NuGet version (EFCore.SqlServer.NodaTime)](https://img.shields.io/nuget/v/SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.svg)](https://www.nuget.org/packages/SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime/)

# EntityFrameworkCore.SqlServer.NodaTime

Adds native support to EntityFrameworkCore for SQL Server for the [NodaTime](https://nodatime.org/) types.

Read more at [Learn Entity Framework Core](https://www.learnentityframeworkcore.com/extensions/entityframeworkcore-sqlserver-nodatime)

[Version History](https://www.learnentityframeworkcore.com/extensions/entityframeworkcore-sqlserver-nodatime#version-history)

## Storing a Duration

A `Duration` is stored as `time` by default, which only holds 0 to just under 24 hours. To store longer durations, or negative ones, give the property one of these column types:

| Column type | Stores | Range | Precision |
|---|---|---|---|
| `time` (default) | the duration as a time of day | 0 to under 24 hours | 100 ns |
| `bigint` | nanoseconds | about ±292 years | 1 ns, NodaTime's own |
| `int` | whole seconds | about ±68 years | 1 s; a fraction of a second is truncated toward zero |
| `datetime2` | the offset from 1900-01-01, so 25 hours is 1900-01-02 01:00 | about -1899 to +8099 years | 100 ns |
| `nvarchar(28)`, or `varchar`, `nchar` or `char` of at least 28 characters | fixed-width text, such as `00000001:01:00:00.000000000` for 25 hours | all of NodaTime's | 1 ns |

```csharp
// One property:
modelBuilder.Entity<Race>().Property(r => r.Length).HasColumnType("bigint");

// Every Duration in the model:
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    => configurationBuilder.Properties<Duration>().HaveColumnType("bigint");
```

The same queries work with all of them except text:

- comparisons, ordering and `Contains`;
- `Days`, `Hours`, `Minutes`, `Seconds`, `Milliseconds`, `SubsecondNanoseconds` and `Microseconds()`;
- `PlusHours` to `PlusNanoseconds`, `+`, `-`, `Plus`, `Minus`, unary `-`, and `*` or `/` by a whole number;
- `EF.Functions.DateDiffHour` and the other `DateDiff` functions;
- `ExecuteUpdate`.

Arithmetic is done in nanoseconds, so a result can pass 24 hours or go negative even when the column is `time`. Two Durations stored differently, such as a `time` column and a `bigint` column, can be compared or combined: both are converted to nanoseconds first, or, for `time` and `datetime2`, the `time` to a `datetime2`.

Things to know:

- **`int`:** a value is truncated to whole seconds when it's saved. So is a parameter or constant that a query compares with an `int` column, so that an index on the column can still be used. A query that does this logs the warning `SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds`, which `options.ConfigureWarnings(w => w.Ignore(SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds))` turns off. The values in a `Contains` list are truncated the same way, without a warning.
- **Text:** only `==`, `!=`, `Equals` and `Contains` are translated, and `ExecuteUpdate` with a value. Anything else, such as `<`, `Hours` or `PlusHours`, can't be computed from text in SQL and isn't translated. Sorting, `Min` and `Max` are only right for values that aren't negative: every value has the same width, so text order is duration order until a minus sign comes in.
- **`time`:** a value of 24 hours or more, or a negative one, can't be stored. Saving one fails, and so does an `ExecuteUpdate` that would write one, or comparing a `time` column with one in a parameter.
- **Range:** a result outside about ±292 years fails with an arithmetic overflow error from SQL Server. A `datetime2` can hold more than that. Storing it, its `Days`, `Hours` and so on, and comparing it with a `time`, `int` or `datetime2` work for its whole range. Arithmetic on it, and comparing it with a `bigint`, are limited to ±292 years.
- **Not translated**, so evaluated in .NET where EF Core allows it: a Duration multiplied or divided by a `double`, and one Duration divided by another.
- **Not converted:** Durations stored differently are not converted to match in join keys, `Union` or `Concat`, or global query filters. Keep those to columns stored the same way. Text is never converted to or from another storage, so it can't be compared or combined with one.

### Changing an existing `time` column

To `datetime2`, the migration EF Core generates just works: SQL Server converts a `time` to that time on 1900-01-01, which is how a `datetime2` stores a Duration.

To `bigint`, `int` or text, it doesn't: SQL Server can't convert `time` to `bigint`, and the text it makes from a `time` isn't the stored format. Replace the `AlterColumn` that EF Core generates for the change with:

```csharp
migrationBuilder.AddColumn<long>(name: "LengthNanoseconds", table: "Races", type: "bigint", nullable: false, defaultValue: 0L);
migrationBuilder.Sql("UPDATE [Races] SET [LengthNanoseconds] = DATEDIFF_BIG(nanosecond, '00:00:00', [Length])");
migrationBuilder.DropColumn(name: "Length", table: "Races");
migrationBuilder.RenameColumn(name: "LengthNanoseconds", table: "Races", newName: "Length");
```

For `int`, use `AddColumn<int>`, `type: "int"`, `defaultValue: 0` and `DATEDIFF(second, '00:00:00', [Length])`. For text, use `AddColumn<string>`, `type: "nvarchar(28)"`, `defaultValue: ""` and `N'00000000:' + CONVERT(nvarchar(16), CAST([Length] AS time(7))) + N'00'`. For a nullable property, use `nullable: true` and leave out `defaultValue`.

## ZonedDateTime, DateTimeZone and Offset

A `ZonedDateTime` property needs no configuration. It is stored in two columns, named the way EF Core names a complex type's:

| Column | Type | Holds |
|---|---|---|
| `Start` | `datetimeoffset` | the local date and time with its offset, so the exact instant |
| `Start_Zone` | `nvarchar(50)` | the time zone ID, such as `Europe/Paris` |

A `ZonedDateTime?` gets nullable columns, and one inside your own complex type gets its path, such as `Schedule_Reminder` and `Schedule_Reminder_Zone`. A `DateTimeZone` property is stored as its ID in `nvarchar(50)`, or in another string type set with `HasColumnType`. An `Offset` is stored as its seconds in an `int`.

To store a `ZonedDateTime`'s zone in another string type (tzdb IDs are at most 32 characters), use `HasColumnType`, or `HasMaxLength` and `IsUnicode(false)`:

```csharp
modelBuilder.Entity<Event>().ComplexProperty(x => x.Start, b => b.Property(z => z.Zone).HasColumnType("varchar(32)"));
```

Time zone IDs are looked up in the tzdb provider. To use another one, such as the operating system's zones:

```csharp
options.UseSqlServer(connectionString, x => x.UseNodaTime(n => n.UseDateTimeZoneProvider(DateTimeZoneProviders.Bcl)));
```

Pass the same provider instance every time, as `DateTimeZoneProviders.Tzdb` and `.Bcl` are: EF Core builds its internal services for each instance. With `UseInternalServiceProvider`, every context using that service provider must use the same time zone provider.

Queries can use:

- `==` (the same instant in the same zone) and `== null`;
- `ToInstant()`, in comparisons and ordering. A comparison with an `Instant` compares the `datetimeoffset` column itself, so an index on it can be used. NodaTime has no `<` or `>` for `ZonedDateTime`, so compare `ToInstant()`;
- `ToOffsetDateTime()`, `LocalDateTime`, `Offset`, `Zone`, `Zone.Id`, and `Year`, `Hour`, `Date` and the other parts of the local date and time.

`ToInstant()`, `LocalDateTime` and `Offset` now translate for an `OffsetDateTime` property too.

Things to know:

- **Precision:** 100 ns, as for any `datetimeoffset`. Only the ISO calendar is kept, as for the other NodaTime types.
- **Time zone rules:** if a zone's rules change after a value was saved (a new tzdb release, for example), the value read back keeps the offset and instant it was saved with.
- **Zone IDs:** values must be read with the provider they were saved with. A zone ID the provider doesn't know fails when the value is read. A compiled model only supports the tzdb and BCL providers.
- **JSON columns:** in a JSON column, a `ZonedDateTime` is an object with its `OffsetDateTime` and `Zone`, and the same queries work. A list of `ZonedDateTime` values isn't supported.
- **Seeding:** `HasData` can't seed a `ZonedDateTime`, since EF Core can't seed complex types.
- **Methods in a projection:** EF Core can't run a method on a `ZonedDateTime` (or any struct complex type) at the end of a query, such as `Select(x => x.Start.WithZone(zone))`. Select the `ZonedDateTime` and call the method afterwards.
- **A default `ZonedDateTime`** (0001-01-01, with no zone set) is saved and read back as UTC, the zone NodaTime gives it.
- **Owned types:** EF Core can't query a complex type on an owned entity type, so a `ZonedDateTime` there fails when the model is built, with a message saying what to do: make the owned type a complex type instead (`ComplexProperty` rather than `OwnsOne`), or map the property yourself.
- **How it's mapped:** through `ZonedDateTime`'s two private fields, its `OffsetDateTime` and its zone. A property you've already mapped yourself keeps your mapping: one with a value converter, on the property or for every `ZonedDateTime` with `Properties<ZonedDateTime>().HaveConversion(...)` in `ConfigureConventions`, or one ignored, including with `IgnoreAny<ZonedDateTime>()`.

## Period

A `Period` property needs no configuration. NodaTime keeps each of a period's units as it is, so `PT90M` isn't the same period as `PT1H30M`, and `P1D` isn't `PT24H`. So the column keeps all ten of them, signs included, as JSON:

```json
{"Years":0,"Months":0,"Weeks":0,"Days":1,"Hours":-1,"Minutes":90,"Seconds":0,"Milliseconds":0,"Ticks":0,"Nanoseconds":0}
```

The column is named after the property and is `nvarchar(max)`. A `Period` inside a JSON column already is an object there. A `Period` inside a complex type of your own that's stored in the table's columns gets a column for each unit (`Rule_Every_Years` and so on) instead, as EF Core can't put a JSON column there.

Queries can use:

- `==`, `!=` and `== null`, which compare unit by unit, as NodaTime does;
- each unit (`Years`, `Months`, ... `Nanoseconds`), and `HasDateComponent` and `HasTimeComponent`;
- adding a period to a `LocalDateTime`, `LocalDate` or `LocalTime`: `+`, `-`, `Plus`, `Minus`, `Add` and `Subtract`, whether the period is a column, a parameter or a constant. Like NodaTime, this adds the years, then the months (from the 31st, a shorter month ends on its last day), then the weeks and days, then the time. A `LocalTime` wraps around midnight;
- `ToDuration()`, counting a week as 7 days and a day as 24 hours;
- one unit between two values, such as an age: `Period.Between(x.BirthDate, today, PeriodUnits.Years).Years`, for any single unit, for `LocalDate`, `LocalDateTime` and `LocalTime` values; and `Period.DaysBetween`.

A null period gives NULL, as a null value does elsewhere in a query, and has no date or time units: its `HasDateComponent` and `HasTimeComponent` are false. Where NodaTime throws for a period, so does the query, rather than give another answer. That covers adding time units to a `LocalDate`, adding date units to a `LocalTime`, and `ToDuration()` with years or months. The SQL Server error includes NodaTime's reason, for example "Conversion failed when converting the nvarchar value 'A Period with time units can't be added to a LocalDate.' to data type int".

Things to know:

- **Precision and range:** added to a value, the time units are kept to 100 ns, as a `datetime2` or `time` keeps them. `ToDuration()` goes up to about 292 years, as `Duration` arithmetic in a query does.
- **Not supported:** `Contains` over a list of periods, comparing a period in a JSON column with one in columns of its own, and periods computed in a query, such as `Period.Between(a, b)` with several units. As for `ZonedDateTime`, `HasData` can't seed a `Period`, and it fails on an owned entity type, saying what to do instead.
- **How it's mapped:** as a complex type over its ten units. EF Core creates a `Period` with its internal constructor, which takes them. As for `ZonedDateTime`, a property you've mapped yourself keeps your mapping, whether with a value converter or `IgnoreAny<Period>()`.

## Sponsors

[Entity Framework Extensions](https://entityframework-extensions.net/) and [Dapper Plus](https://dapper-plus.net/) are major sponsors and proud to contribute to the development of **EFCore.SqlServer.NodaTime**.

[![Entity Framework Extensions](https://raw.githubusercontent.com/StevenRasmussen/EFCore.SqlServer.NodaTime/master/entity-framework-extensions-sponsor.png)](https://entityframework-extensions.net/bulk-insert)

[![Dapper Plus](https://raw.githubusercontent.com/StevenRasmussen/EFCore.SqlServer.NodaTime/master/dapper-plus-sponsor.png)](https://dapper-plus.net/bulk-insert)

