using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Diagnostics
{
    internal static class SqlServerNodaTimeLoggerExtensions
    {
        /// <summary>
        /// Logs <see cref="SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds"/> through EF Core, so that
        /// ConfigureWarnings can ignore it or turn it into an exception.
        /// </summary>
        public static void DurationValueTruncatedToSeconds(this IDiagnosticsLogger<DbLoggerCategory.Query> logger, string column)
        {
            if (logger == null)
                return;

            var definition = new EventDefinition<string>(
                logger.Options,
                SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds,
                LogLevel.Warning,
                nameof(SqlServerNodaTimeEventId) + "." + nameof(SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds),
                level => LoggerMessage.Define<string>(
                    level,
                    SqlServerNodaTimeEventId.DurationValueTruncatedToSeconds,
                    "A parameter or constant is used with the Duration {Column}, which is stored as whole seconds (int), so it is "
                    + "converted to whole seconds too: any fraction of a second in it is dropped, and the result can differ from "
                    + "the same code run in .NET. Use whole seconds, or store the Duration as bigint (nanoseconds)."));

            if (logger.ShouldLog(definition))
                definition.Log(logger, column);
        }
    }
}
