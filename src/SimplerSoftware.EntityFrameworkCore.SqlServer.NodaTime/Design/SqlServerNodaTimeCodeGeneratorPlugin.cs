using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Scaffolding;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design
{
    public class SqlServerNodaTimeCodeGeneratorPlugin : ProviderCodeGeneratorPlugin
    {
        public override MethodCallCodeFragment GenerateProviderOptions()
        {
            // UseNodaTime() without options: there's an overload that takes them.
            var methodInfo = typeof(SqlServerDbContextOptionsBuilderExtensions).GetMethod(nameof(SqlServerDbContextOptionsBuilderExtensions.UseNodaTime), [typeof(SqlServerDbContextOptionsBuilder)]);
            return new MethodCallCodeFragment(methodInfo);
        }
    }
}
