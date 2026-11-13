using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Diagnostics
{
    /// <summary>
    /// The IDs of the warnings this library logs. Use them with ConfigureWarnings, for example
    /// <c>options.ConfigureWarnings(w =&gt; w.Ignore(SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds))</c>.
    /// </summary>
    public static class SqlServerNodaTimeEventId
    {
        /// <summary>
        /// A query uses a parameter or constant with a Duration stored as int (whole seconds), for example in a
        /// comparison. The value is converted to whole seconds too, so any fraction of a second in it is dropped and
        /// the result can differ from .NET's. Logged when the query is compiled: for a parameter, whose value isn't known
        /// then, and for a constant that has a fraction of a second.
        /// </summary>
        public static readonly EventId DurationValueTruncatedToSeconds
            = new EventId(40001, "SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.DurationValueTruncatedToSeconds");
    }
}
