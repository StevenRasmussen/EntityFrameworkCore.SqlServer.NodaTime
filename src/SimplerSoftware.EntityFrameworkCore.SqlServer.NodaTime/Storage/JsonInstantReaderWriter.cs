using Microsoft.EntityFrameworkCore.Storage.Json;
using NodaTime;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.EntityFrameworkCore.SqlServer.Storage
{
    /// <summary>
    /// Reads and writes <see cref="Instant"/> values in JSON: list properties, JSON columns, and parameter lists
    /// such as list.Contains(x.Instant).
    /// </summary>
    /// <remarks>
    /// Writes "2019-02-01T10:00:00Z", the same as EF Core writes a UTC <see cref="DateTime"/>.
    /// Reads any ISO 8601 date/time: "Z" and numeric offsets such as "+05:30" are honoured, and a value with no
    /// offset is taken as UTC, the same as <see cref="InstantValueConverter"/> does for values read from columns.
    /// </remarks>
    public sealed class JsonInstantReaderWriter : JsonValueReaderWriter<Instant>
    {
        private static readonly PropertyInfo InstanceProperty = typeof(JsonInstantReaderWriter).GetProperty(nameof(Instance));

        public static JsonInstantReaderWriter Instance { get; } = new();

        private JsonInstantReaderWriter()
        {
        }

        public override Instant FromJsonTyped(ref Utf8JsonReaderManager manager, object existingObject = null)
        {
            var dateTime = manager.CurrentReader.GetDateTime();

            // GetDateTime() converts a value with a numeric offset (e.g. "+05:30") to the machine's local time,
            // so read the offset itself instead. "Z" gives a UTC DateTime and no offset gives an unspecified one.
            return dateTime.Kind == DateTimeKind.Local
                ? Instant.FromDateTimeOffset(manager.CurrentReader.GetDateTimeOffset())
                : Instant.FromDateTimeUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
        }

        public override void ToJsonTyped(Utf8JsonWriter writer, Instant value)
            => writer.WriteStringValue(value.ToDateTimeUtc());

        /// <summary>Lets compiled models (dotnet ef dbcontext optimize) refer to the shared instance.</summary>
        public override Expression ConstructorExpression
            => Expression.Property(null, InstanceProperty);
    }
}
