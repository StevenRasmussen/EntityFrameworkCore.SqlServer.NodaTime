using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.ExpressionTranslators
{
    public class NodaTimeTypesMemberTranslatorPlugin : SqlServerMemberTranslatorProvider
    {
        public NodaTimeTypesMemberTranslatorPlugin(
            [NotNull] RelationalMemberTranslatorProviderDependencies dependencies,
            IRelationalTypeMappingSource typeMappingSource)
            : base(dependencies, typeMappingSource)
        {
            var sqlExpressionFactory = dependencies.SqlExpressionFactory;

            AddTranslators(
                new IMemberTranslator[]
                {
                    new LocalDateMemberTranslator(sqlExpressionFactory),
                    new LocalTimeMemberTranslator(sqlExpressionFactory),
                    new LocalDateTimeMemberTranslator(sqlExpressionFactory),
                    new DurationMemberTranslator(sqlExpressionFactory),
                    new OffsetDateTimeMemberTranslator(sqlExpressionFactory),
                    new ZonedDateTimeTranslator(sqlExpressionFactory, typeMappingSource),
                });
        }
    }
}
