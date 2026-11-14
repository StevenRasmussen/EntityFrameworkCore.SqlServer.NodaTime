using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding;
using NodaTime;
using SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Design;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime.Tests
{
    /// <summary>Compiles the code that EF Core's design-time tools generate, and loads it.</summary>
    internal static class GeneratedCode
    {
        public static Assembly Compile(string assemblyName, IEnumerable<ScaffoldedFile> files)
        {
            // The assemblies the tests run with (the framework and the packages), and the ones the generated code uses.
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Concat(new[]
                {
                    typeof(GeneratedCode).Assembly.Location,
                    typeof(SqlServerNodaTimeDesignTimeServices).Assembly.Location,
                    typeof(Duration).Assembly.Location,
                    typeof(DbContext).Assembly.Location,
                    typeof(RelationalDatabaseFacadeExtensions).Assembly.Location,
                    typeof(SqlServerDbContextOptionsExtensions).Assembly.Location,
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(
                assemblyName,
                files.Select(f => CSharpSyntaxTree.ParseText(f.Code, path: f.Path)),
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            using var assembly = new MemoryStream();
            var result = compilation.Emit(assembly);
            Assert.True(
                result.Success,
                "The generated code doesn't compile:" + Environment.NewLine
                + string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

            return Assembly.Load(assembly.ToArray());
        }
    }
}
