using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    internal class OffsetDateTimeMethodCallTranslatorPlugin : IMethodCallTranslatorPlugin
    {
        public OffsetDateTimeMethodCallTranslatorPlugin(
            ISqlExpressionFactory sqlExpressionFactory,
            IRelationalTypeMappingSource typeMappingSource)
        {
            Translators = new IMethodCallTranslator[]
            {
                new OffsetDateTimeMethodTranslator(sqlExpressionFactory),
                new ZonedDateTimeTranslator(sqlExpressionFactory, typeMappingSource),
            };
        }

        public virtual IEnumerable<IMethodCallTranslator> Translators { get; }
    }
}
