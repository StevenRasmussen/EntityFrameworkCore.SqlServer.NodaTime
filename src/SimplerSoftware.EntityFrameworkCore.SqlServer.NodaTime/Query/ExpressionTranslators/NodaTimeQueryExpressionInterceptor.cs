using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// EF Core translates operators by kind and ignores user-defined operator methods, so LocalDate + LocalTime would
    /// become SQL "date + time", which SQL Server rejects. This rewrites the operator into a call to its operator
    /// method, which <see cref="LocalDateTimeCombineTranslator"/> translates (#48).
    /// </summary>
    internal sealed class NodaTimeQueryExpressionInterceptor : IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
            => new OperatorRewriter().Visit(queryExpression);

        private sealed class OperatorRewriter : ExpressionVisitor
        {
            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.Method != LocalDateTimeCombineTranslator.LocalDatePlusLocalTime)
                    return base.VisitBinary(node);

                var left = Visit(node.Left);
                var right = Visit(node.Right);

                if (!node.IsLifted)
                    return Expression.Call(node.Method, left, right);

                // LocalDate? + LocalTime?: the SQL is NULL when either side is, which matches the lifted operator.
                return Expression.Convert(
                    Expression.Call(node.Method, Expression.Convert(left, typeof(LocalDate)), Expression.Convert(right, typeof(LocalTime))),
                    typeof(LocalDateTime?));
            }
        }
    }
}
