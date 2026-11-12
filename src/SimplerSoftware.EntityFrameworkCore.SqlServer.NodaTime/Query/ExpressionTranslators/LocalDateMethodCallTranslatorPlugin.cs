using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    internal class LocalDateMethodCallTranslatorPlugin : IMethodCallTranslatorPlugin
    {
        public LocalDateMethodCallTranslatorPlugin(
            ISqlExpressionFactory sqlExpressionFactory)
        {
            Translators = new IMethodCallTranslator[]
            {
                new LocalDateMethodTranslator(sqlExpressionFactory),
                new LocalDateTimeCombineTranslator(sqlExpressionFactory),
            };
        }

        public virtual IEnumerable<IMethodCallTranslator> Translators { get; }
    }
}
