using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using System.Reflection;

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
    /// <item>A ZonedDateTime's parts become parts of the OffsetDateTime it is mapped as (#22), and comparing an
    /// OffsetDateTime's instant with an Instant becomes a call to <see cref="InstantComparisons"/>.</item>
    /// </list>
    /// </summary>
    internal sealed class NodaTimeQueryExpressionInterceptor : IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
            => new OperatorRewriter(eventData.Context?.Model is { } model ? ZonedDateTimeProperties(model) : null).Visit(queryExpression);

        // The ZonedDateTime properties each model maps as complex properties (#22), by the CLR type that declares them.
        private static readonly ConditionalWeakTable<IModel, (Type DeclaringType, string Name)[]> _zonedDateTimeProperties = new();

        private static (Type DeclaringType, string Name)[] ZonedDateTimeProperties(IModel model)
            => _zonedDateTimeProperties.GetValue(model, m => m.GetEntityTypes().SelectMany(ZonedDateTimeProperties).Distinct().ToArray());

        private static IEnumerable<(Type DeclaringType, string Name)> ZonedDateTimeProperties(ITypeBase type)
            => type.GetDeclaredComplexProperties().SelectMany(complexProperty => complexProperty.ComplexType.ClrType == typeof(ZonedDateTime)
                ? new[] { (complexProperty.DeclaringType.ClrType, complexProperty.Name) }
                : ZonedDateTimeProperties(complexProperty.ComplexType));

        private sealed class OperatorRewriter : ExpressionVisitor
        {
            // Null when there's no model to look in: then any ZonedDateTime property is taken to be mapped.
            private readonly (Type DeclaringType, string Name)[] _zonedDateTimeProperties;

            public OperatorRewriter((Type DeclaringType, string Name)[] zonedDateTimeProperties)
            {
                _zonedDateTimeProperties = zonedDateTimeProperties;
            }

            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.Method == LocalDateTimeCombineTranslator.LocalDatePlusLocalTime)
                    return RewriteLocalDatePlusLocalTime(node);

                if (node.Method?.DeclaringType == typeof(Duration))
                    return RewriteDurationOperator(node) ?? base.VisitBinary(node);

                if (node.Method?.DeclaringType == typeof(Instant) && node.Type == typeof(bool))
                {
                    var left = Visit(node.Left);
                    var right = Visit(node.Right);
                    return RewriteInstantComparison(node, left, right) ?? node.Update(left, node.Conversion, right);
                }

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

            // A ZonedDateTime is mapped as its OffsetDateTime and its Zone (#22), so its other parts are rewritten in terms
            // of the OffsetDateTime: z.ToInstant() is z's OffsetDateTime's ToInstant(), z.Year its OffsetDateTime's Year.
            private static readonly MethodInfo _efProperty = typeof(EF).GetMethod(nameof(EF.Property))!;
            private static readonly MethodInfo _efPropertyOffsetDateTime = _efProperty.MakeGenericMethod(typeof(OffsetDateTime));
            private static readonly MethodInfo _offsetDateTimeToInstant = typeof(OffsetDateTime).GetMethod(nameof(OffsetDateTime.ToInstant), Type.EmptyTypes)!;

            private static Expression OffsetDateTimeOf(Expression zoned)
                => Expression.Call(
                    _efPropertyOffsetDateTime,
                    Expression.Convert(zoned, typeof(object)),
                    Expression.Constant(SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata.ZonedDateTimeConvention.OffsetDateTimePropertyName));

            protected override Expression VisitMember(MemberExpression node)
            {
                // LocalDateTime, Offset, Year, Hour, Date... are the same as its OffsetDateTime's. Zone is mapped itself, so it
                // needs no rewriting.
                if (node.Expression?.Type == typeof(ZonedDateTime) && node.Member.Name != nameof(ZonedDateTime.Zone) && IsMapped(node.Expression))
                {
                    var offsetDateTimeMember = typeof(OffsetDateTime).GetProperty(node.Member.Name);
                    if (offsetDateTimeMember != null && offsetDateTimeMember.PropertyType == node.Type)
                        return Expression.Property(OffsetDateTimeOf(Visit(node.Expression)), offsetDateTimeMember);
                }

                return base.VisitMember(node);
            }

            /// <summary>
            /// Whether a ZonedDateTime is a property the model maps as a complex property, with columns, perhaps through
            /// .Value or EF.Property. Not one computed in the query, such as (x.End ?? x.Start), or one the application maps
            /// itself, with a value converter, which EF Core can still evaluate in .NET at the end of a query.
            /// </summary>
            private bool IsMapped(Expression zoned)
            {
                zoned = StripConvert(zoned);
                if (zoned is MemberExpression { Member.Name: nameof(Nullable<ZonedDateTime>.Value) } value && value.Expression?.Type == typeof(ZonedDateTime?))
                    zoned = StripConvert(value.Expression);

                return zoned switch
                {
                    MemberExpression { Expression: { } instance } member => IsMappedProperty(instance.Type, member.Member.Name),
                    MethodCallExpression { Arguments: [var instance, ConstantExpression { Value: string name }] } call
                        when call.Method.IsGenericMethod && call.Method.GetGenericMethodDefinition() == _efProperty
                        => IsMappedProperty(StripConvert(instance).Type, name),
                    _ => false,
                };
            }

            // Declared on the type or a base type (a ZonedDateTime of a base entity type, in a query of a derived one).
            private bool IsMappedProperty(Type type, string name)
                => _zonedDateTimeProperties == null
                    || _zonedDateTimeProperties.Any(p => p.Name == name && p.DeclaringType.IsAssignableFrom(type));

            /// <summary>
            /// offsetDateTime.ToInstant() OP instant, either way round: the stand-in compares the datetimeoffset itself,
            /// which can use an index. Null if the comparison isn't one of these.
            /// </summary>
            private static Expression RewriteInstantComparison(BinaryExpression node, Expression left, Expression right)
            {
                var operatorType = node.NodeType;
                Expression offsetDateTime;
                Expression instant;
                if (AsToInstant(left) is { } leftOffsetDateTime && IsInstant(right.Type))
                {
                    (offsetDateTime, instant) = (leftOffsetDateTime, right);
                }
                else if (AsToInstant(right) is { } rightOffsetDateTime && IsInstant(left.Type))
                {
                    (offsetDateTime, instant) = (rightOffsetDateTime, left);
                    operatorType = InstantComparisons.Mirror(operatorType);
                }
                else
                {
                    return null;
                }

                var nullable = node.IsLifted;
                var standIn = InstantComparisons.Find(operatorType, nullable);
                if (standIn == null || standIn.ReturnType != node.Type)
                    return null;

                var parameters = standIn.GetParameters();
                return Expression.Call(standIn, AsType(offsetDateTime, parameters[0].ParameterType), AsType(instant, parameters[1].ParameterType));
            }

            /// <summary>The OffsetDateTime in offsetDateTime.ToInstant(), or null.</summary>
            private static Expression AsToInstant(Expression expression)
                => StripConvert(expression) is MethodCallExpression call && call.Method == _offsetDateTimeToInstant ? call.Object : null;

            private static bool IsInstant(Type type) => type == typeof(Instant) || type == typeof(Instant?);

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Object?.Type == typeof(ZonedDateTime) && node.Arguments.Count == 0 && IsMapped(node.Object))
                {
                    switch (node.Method.Name)
                    {
                        case nameof(ZonedDateTime.ToOffsetDateTime):
                            return OffsetDateTimeOf(Visit(node.Object));
                        case nameof(ZonedDateTime.ToInstant):
                            return Expression.Call(OffsetDateTimeOf(Visit(node.Object)), _offsetDateTimeToInstant);
                    }
                }

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
