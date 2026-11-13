using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    public abstract class BaseNodaTimeMethodCallTranslator : IMethodCallTranslator
    {
        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        private readonly Dictionary<MethodInfo, string> _methodInfoDateAddMapping;
        private readonly Dictionary<MethodInfo, string> _methodInfoDateAddExtensionMapping;
        private readonly Dictionary<MethodInfo, string> _methodInfoDatePartExtensionMapping;
        private readonly Dictionary<MethodInfo, string> _methodInfoDateDiffMapping;
        private readonly Dictionary<MethodInfo, string> _methodInfoDateDiffBigMapping;


        public BaseNodaTimeMethodCallTranslator(
            ISqlExpressionFactory sqlExpressionFactory,
            Dictionary<MethodInfo, string> methodInfoDateAddMapping,
            Dictionary<MethodInfo, string> methodInfoDateAddExtensionMapping,
            Dictionary<MethodInfo, string> methodInfoDatePartExtensionMapping,
            Dictionary<MethodInfo, string> methodInfoDateDiffMapping,
            Dictionary<MethodInfo, string> methodInfoDateDiffBigMapping)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
            _methodInfoDateAddMapping = methodInfoDateAddMapping;
            _methodInfoDateAddExtensionMapping = methodInfoDateAddExtensionMapping;
            _methodInfoDatePartExtensionMapping = methodInfoDatePartExtensionMapping;
            _methodInfoDateDiffMapping = methodInfoDateDiffMapping;
            _methodInfoDateDiffBigMapping = methodInfoDateDiffBigMapping;
        }

        public virtual SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (_methodInfoDateAddMapping != null && _methodInfoDateAddMapping.TryGetValue(method, out var dateAddPart))
            {
                return arguments[0] is SqlConstantExpression sqlConstant
                    && ((sqlConstant.Value is double && ((double)sqlConstant.Value >= int.MaxValue || (double)sqlConstant.Value <= int.MinValue))
                    || (sqlConstant.Value is long && ((long)sqlConstant.Value >= int.MaxValue || (long)sqlConstant.Value <= int.MinValue)))
                        ? null
                        : _sqlExpressionFactory.Function(
                            name: "DATEADD",
                            arguments: new[]
                            {
                                new SqlFragmentExpression(dateAddPart),
                                _sqlExpressionFactory.Convert(arguments[0], typeof(int)),
                                instance
                            },
                            nullable: true,
                            argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                            returnType: instance.Type,
                            typeMapping: instance.TypeMapping);
            }
            else if (_methodInfoDateAddExtensionMapping != null && _methodInfoDateAddExtensionMapping.TryGetValue(method, out var dateAddExtensionPart))
            {
                return arguments[1] is SqlConstantExpression sqlConstant
                    && ((sqlConstant.Value is double && ((double)sqlConstant.Value >= int.MaxValue || (double)sqlConstant.Value <= int.MinValue))
                    || (sqlConstant.Value is long && ((long)sqlConstant.Value >= int.MaxValue || (long)sqlConstant.Value <= int.MinValue)))
                        ? null
                        : _sqlExpressionFactory.Function(
                            name: "DATEADD",
                            arguments: new[]
                            {
                                new SqlFragmentExpression(dateAddExtensionPart),
                                _sqlExpressionFactory.Convert(arguments[1], typeof(int)),
                                arguments[0]
                            },
                            nullable: true,
                            argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                            returnType: arguments[0].Type,
                            typeMapping: arguments[0].TypeMapping);
            }
            else if (_methodInfoDatePartExtensionMapping != null && _methodInfoDatePartExtensionMapping.TryGetValue(method, out var datePart))
            {
                return _sqlExpressionFactory.Function(
                            name: "DATEPART",
                            arguments: new[]
                            {
                                new SqlFragmentExpression(datePart),
                                arguments[0]
                            },
                            nullable: true,
                            argumentsPropagateNullability: SqlServerFunctionNullability.DatePart,
                            returnType: method.ReturnType,
                            typeMapping: null);
            }
            else if (_methodInfoDateDiffMapping != null && _methodInfoDateDiffMapping.TryGetValue(method, out var dateDiffDatePart))
            {
                var startDate = arguments[1];
                var endDate = arguments[2];
                var typeMapping = ExpressionExtensions.InferTypeMapping(startDate, endDate);

                startDate = _sqlExpressionFactory.ApplyTypeMapping(startDate, typeMapping);
                endDate = _sqlExpressionFactory.ApplyTypeMapping(endDate, typeMapping);

                return _sqlExpressionFactory.Function(
                    name: "DATEDIFF",
                    arguments: new[] { new SqlFragmentExpression(dateDiffDatePart), startDate, endDate },
                    nullable: true,
                    argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                    returnType: method.ReturnType,
                    typeMapping: null);
            }
            else if (_methodInfoDateDiffBigMapping != null && _methodInfoDateDiffBigMapping.TryGetValue(method, out var dateDiffBigDatePart))
            {
                var startDate = arguments[1];
                var endDate = arguments[2];
                var typeMapping = ExpressionExtensions.InferTypeMapping(startDate, endDate);

                startDate = _sqlExpressionFactory.ApplyTypeMapping(startDate, typeMapping);
                endDate = _sqlExpressionFactory.ApplyTypeMapping(endDate, typeMapping);

                return _sqlExpressionFactory.Function(
                    name: "DATEDIFF_BIG",
                    arguments: new[] { new SqlFragmentExpression(dateDiffBigDatePart), startDate, endDate },
                    nullable: true,
                    argumentsPropagateNullability: SqlServerFunctionNullability.DateAddOrDiff,
                    returnType: method.ReturnType,
                    typeMapping: null);
            }
            return null;
        }
    }
}
