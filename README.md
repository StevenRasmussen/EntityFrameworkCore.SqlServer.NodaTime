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

```csharp
// One property:
modelBuilder.Entity<Race>().Property(r => r.Length).HasColumnType("bigint");

// Every Duration in the model:
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    => configurationBuilder.Properties<Duration>().HaveColumnType("bigint");
```

The same queries work with all four:

- comparisons, ordering and `Contains`;
- `Days`, `Hours`, `Minutes`, `Seconds`, `Milliseconds`, `SubsecondNanoseconds` and `Microseconds()`;
- `PlusHours` to `PlusNanoseconds`, `+`, `-`, `Plus`, `Minus`, unary `-`, and `*` or `/` by a whole number;
- `EF.Functions.DateDiffHour` and the other `DateDiff` functions;
- `ExecuteUpdate`.

Arithmetic is done in nanoseconds, so a result can pass 24 hours or go negative even when the column is `time`. Two Durations stored differently, such as a `time` column and a `bigint` column, can be compared or combined: both are converted to nanoseconds first, or, for `time` and `datetime2`, the `time` to a `datetime2`.

Things to know:

- **`int`:** a value is truncated to whole seconds when it's saved. So is a parameter or constant that a query compares with an `int` column, so that an index on the column can still be used. A query that does this logs the warning `SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds`, which `options.ConfigureWarnings(w => w.Ignore(SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds))` turns off. The values in a `Contains` list are truncated the same way, without a warning.
- **`time`:** a value of 24 hours or more, or a negative one, can't be stored. Saving one fails, and so does an `ExecuteUpdate` that would write one, or comparing a `time` column with one in a parameter.
- **Range:** a result outside about ±292 years fails with an arithmetic overflow error from SQL Server. A `datetime2` can hold more than that. Storing it, its `Days`, `Hours` and so on, and comparing it with a `time`, `int` or `datetime2` work for its whole range. Arithmetic on it, and comparing it with a `bigint`, are limited to ±292 years.
- **Not translated**, so evaluated in .NET where EF Core allows it: a Duration multiplied or divided by a `double`, and one Duration divided by another.
- **Not converted:** Durations stored differently are not converted to match in join keys, `Union` or `Concat`, or global query filters. Keep those to columns stored the same way.

### Changing an existing `time` column

To `datetime2`, the migration EF Core generates just works: SQL Server converts a `time` to that time on 1900-01-01, which is how a `datetime2` stores a Duration.

To `bigint` or `int`, it doesn't: SQL Server can't convert `time` to `bigint`, so the `AlterColumn` that EF Core generates for the change fails. Replace it in the migration with:

```csharp
migrationBuilder.AddColumn<long>(name: "LengthNanoseconds", table: "Races", type: "bigint", nullable: false, defaultValue: 0L);
migrationBuilder.Sql("UPDATE [Races] SET [LengthNanoseconds] = DATEDIFF_BIG(nanosecond, '00:00:00', [Length])");
migrationBuilder.DropColumn(name: "Length", table: "Races");
migrationBuilder.RenameColumn(name: "LengthNanoseconds", table: "Races", newName: "Length");
```

For `int`, use `AddColumn<int>`, `type: "int"`, `defaultValue: 0` and `DATEDIFF(second, '00:00:00', [Length])`. For a nullable property, use `nullable: true` and leave out `defaultValue`.

## Sponsors

[Entity Framework Extensions](https://entityframework-extensions.net/) and [Dapper Plus](https://dapper-plus.net/) are major sponsors and proud to contribute to the development of **EFCore.SqlServer.NodaTime**.

[![Entity Framework Extensions](https://raw.githubusercontent.com/StevenRasmussen/EFCore.SqlServer.NodaTime/master/entity-framework-extensions-sponsor.png)](https://entityframework-extensions.net/bulk-insert)

[![Dapper Plus](https://raw.githubusercontent.com/StevenRasmussen/EFCore.SqlServer.NodaTime/master/dapper-plus-sponsor.png)](https://dapper-plus.net/bulk-insert)

