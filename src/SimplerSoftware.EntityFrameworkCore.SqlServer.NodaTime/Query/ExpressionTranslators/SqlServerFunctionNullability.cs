namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// Which arguments of SQL Server's date functions make the result NULL when they are NULL, for
    /// ISqlExpressionFactory.Function's argumentsPropagateNullability. The first argument is always the date part
    /// (hour, day...), which is never NULL. These match what EF Core uses for its own DateTime translations; marking a
    /// function as never NULL instead makes EF Core get comparisons with NULL values wrong (x.Nullable.Value.Year != 2000
    /// would leave out the NULL rows).
    /// </summary>
    internal static class SqlServerFunctionNullability
    {
        /// <summary>DATEPART(part, date).</summary>
        public static readonly bool[] DatePart = { false, true };

        /// <summary>DATEADD(part, number, date), and DATEDIFF(part, start, end) or DATEDIFF_BIG.</summary>
        public static readonly bool[] DateAddOrDiff = { false, true, true };

        /// <summary>DATEDIFF(part, '19000101', date) or DATEDIFF_BIG: only the date can be NULL.</summary>
        public static readonly bool[] DateDiffFrom1900 = { false, false, true };
    }
}
