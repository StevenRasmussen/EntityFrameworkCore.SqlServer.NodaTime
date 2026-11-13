using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Metadata
{
    /// <summary>Adds the conventions that map ZonedDateTime (#22) and Period (#3) properties as complex types.</summary>
    internal sealed class NodaTimeConventionSetPlugin : IConventionSetPlugin
    {
        private readonly ProviderConventionSetBuilderDependencies _dependencies;

        public NodaTimeConventionSetPlugin(ProviderConventionSetBuilderDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public ConventionSet ModifyConventions(ConventionSet conventionSet)
        {
            var zonedDateTime = new ZonedDateTimeConvention(_dependencies.MemberClassifier);
            var period = new PeriodConvention(_dependencies.MemberClassifier);
            conventionSet.EntityTypeAddedConventions.Add(zonedDateTime);
            conventionSet.EntityTypeAddedConventions.Add(period);
            conventionSet.ComplexPropertyAddedConventions.Add(zonedDateTime);
            conventionSet.ComplexPropertyAddedConventions.Add(period);
            // First, so that EF Core's own finalizing conventions (such as making column names unique) see the names.
            conventionSet.ModelFinalizingConventions.Insert(0, zonedDateTime);
            conventionSet.ModelFinalizingConventions.Insert(0, period);
            return conventionSet;
        }
    }
}
