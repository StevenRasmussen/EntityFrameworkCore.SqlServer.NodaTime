using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    /// <summary>
    /// EF Core translates operators, ?? and ?: itself and ignores user-defined operator methods, so some NodaTime code
    /// needs rewriting into method calls that the plugin's method translators can translate:
    /// <list type="bullet">
    /// <item>LocalDate + LocalTime would become SQL "date + time", which SQL Server rejects; it becomes a call to its
    /// operator method, translated by <see cref="LocalDateTimeCombineTranslator"/> (#48).</item>
    /// <item>Duration's operators become calls to <see cref="DurationOperators"/>, translated by
    /// <see cref="DurationMethodTranslator"/>, which can then see how each side is stored (#38). ?? and ?: between
    /// Durations are wrapped in one, and so are the values ExecuteUpdate writes into Duration columns.</item>
    /// </list>
    /// </summary>
    internal sealed class NodaTimeQueryExpressionInterceptor : IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
            => new OperatorRewriter().Visit(queryExpression);

        private sealed class OperatorRewriter : ExpressionVisitor
        {
            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.Method == LocalDateTimeCombineTranslator.LocalDatePlusLocalTime)
                    return RewriteLocalDatePlusLocalTime(node);

                if (node.Method?.DeclaringType == typeof(Duration))
                    return RewriteDurationOperator(node) ?? base.VisitBinary(node);

                if (node.NodeType == ExpressionType.Coalesce && IsDuration(node.Type))
                    return SameStorage(base.VisitBinary(node));

                return base.VisitBinary(node);
            }

            protected override Expression VisitUnary(UnaryExpression node)
            {
                if (node.NodeType == ExpressionType.Negate && node.Method?.DeclaringType == typeof(Duration))
                {
                    var standIn = DurationOperators.StandInFor(node.Method, node.IsLifted);
                    if (standIn != null)
                        return Expression.Call(standIn, Visit(node.Operand));
                }

                return base.VisitUnary(node);
            }

            protected override Expression VisitConditional(ConditionalExpression node)
                => IsDuration(node.Type) ? SameStorage(base.VisitConditional(node)) : base.VisitConditional(node);

            /// <summary>
            /// ?? or ?: between Durations: the branches may be stored differently (an int and a bigint column, or a time
            /// column and the result of PlusHours, which is in nanoseconds), and SQL needs them the same. The whole
            /// expression goes in, so that in .NET it still only evaluates the branch it needs.
            /// </summary>
            private static Expression SameStorage(Expression expression)
                => Expression.Call(
                    expression.Type == typeof(Duration?) ? DurationOperators.SameNullableStorageMethod : DurationOperators.SameStorageMethod,
                    expression);

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                var visited = (MethodCallExpression)base.VisitMethodCall(node);

                // duration.Equals(other): EF Core turns it into ==, but only after this runs, so it would skip the stand-in.
                // Equals(object) may be recorded as object's or ValueType's method, so check the instance's type.
                if (visited.Object?.Type == typeof(Duration)
                    && visited.Method.Name == nameof(Duration.Equals)
                    && visited.Arguments.Count == 1)
                {
                    var other = visited.Arguments[0].NodeType == ExpressionType.Convert && visited.Arguments[0].Type == typeof(object)
                        ? ((UnaryExpression)visited.Arguments[0]).Operand
                        : visited.Arguments[0];
                    if (IsDuration(other.Type) && !IsNull(other))
                    {
                        var equal = other.Type == typeof(Duration?) ? DurationOperators.EqualNullableMethod : DurationOperators.EqualMethod;
                        var parameterType = equal.GetParameters()[0].ParameterType;
                        return Expression.Call(equal, AsType(visited.Object, parameterType), AsType(other, parameterType));
                    }
                }

                // ExecuteUpdate(source, new[] { (property selector, value selector), ... })
                if (visited.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                    && visited.Method.Name == nameof(EntityFrameworkQueryableExtensions.ExecuteUpdate)
                    && visited.Arguments.Count == 2
                    && visited.Arguments[1] is NewArrayExpression setters)
                {
                    return visited.Update(visited.Object, new[] { visited.Arguments[0], setters.Update(setters.Expressions.Select(RewriteSetter)) });
                }

                return visited;
            }

            private Expression RewriteLocalDatePlusLocalTime(BinaryExpression node)
            {
                var left = Visit(node.Left);
                var right = Visit(node.Right);

                if (!node.IsLifted)
                    return Expression.Call(node.Method, left, right);

                // LocalDate? + LocalTime?: the SQL is NULL when either side is, which matches the lifted operator.
                return Expression.Convert(
                    Expression.Call(node.Method, Expression.Convert(left, typeof(LocalDate)), Expression.Convert(right, typeof(LocalTime))),
                    typeof(LocalDateTime?));
            }

            private Expression RewriteDurationOperator(BinaryExpression node)
            {
                // Comparing with null (x.Duration == null) is left to EF Core: it becomes IS NULL.
                if (IsNull(node.Left) || IsNull(node.Right))
                    return null;

                var standIn = DurationOperators.StandInFor(node.Method, node.IsLifted);
                if (standIn == null || standIn.ReturnType != node.Type)
                    return null;

                var parameters = standIn.GetParameters();
                return Expression.Call(standIn, AsType(Visit(node.Left), parameters[0].ParameterType), AsType(Visit(node.Right), parameters[1].ParameterType));
            }

            /// <summary>
            /// The value written into a Duration column goes through DurationOperators.ForColumn, which converts it to the
            /// column's storage: e.g. x.Time.PlusHours(1) is computed in nanoseconds and must go back into a time.
            /// </summary>
            private static Expression RewriteSetter(Expression setter)
            {
                if (setter is not NewExpression { Arguments.Count: 2 } tuple)
                    return setter;

                var property = AsLambda(tuple.Arguments[0]);
                var value = AsLambda(tuple.Arguments[1]);
                if (property == null || value == null || !IsDuration(property.Body.Type) || !IsDuration(value.Body.Type))
                    return setter;

                var column = new ReplaceParameter(property.Parameters[0], value.Parameters[0]).Visit(property.Body);
                var nullable = value.Body.Type == typeof(Duration?) || column.Type == typeof(Duration?);
                var forColumn = nullable ? DurationOperators.ForNullableColumnMethod : DurationOperators.ForColumnMethod;
                var parameterType = forColumn.GetParameters()[0].ParameterType;

                var body = AsType(Expression.Call(forColumn, AsType(value.Body, parameterType), AsType(column, parameterType)), value.Body.Type);
                var newValue = Expression.Lambda(value.Type, body, value.Parameters);

                return tuple.Update(new[]
                {
                    tuple.Arguments[0],
                    tuple.Arguments[1].NodeType == ExpressionType.Quote ? Expression.Quote(newValue) : newValue,
                });
            }

            private static LambdaExpression AsLambda(Expression expression) => expression switch
            {
                LambdaExpression lambda => lambda,
                UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
                _ => null,
            };

            private static Expression AsType(Expression expression, Type type)
                => expression.Type == type ? expression : Expression.Convert(expression, type);

            private static bool IsDuration(Type type) => type == typeof(Duration) || type == typeof(Duration?);

            private static Expression StripConvert(Expression expression)
            {
                while (expression.NodeType == ExpressionType.Convert)
                    expression = ((UnaryExpression)expression).Operand;
                return expression;
            }

            private static bool IsNull(Expression expression)
            {
                expression = StripConvert(expression);
                return expression is ConstantExpression { Value: null } || expression is DefaultExpression && expression.Type == typeof(Duration?);
            }
        }

        private sealed class ReplaceParameter(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
        {
            protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : node;
        }
    }
}
