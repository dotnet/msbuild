using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.NET.TestFramework.Assertions
{
    public class DependencyContextAssertions
    {
        private DependencyContext _dependencyContext;

        public DependencyContextAssertions(DependencyContext dependencyContext)
        {
            _dependencyContext = dependencyContext;
        }

        public AndConstraint<DependencyContextAssertions> HaveNoDuplicateRuntimeAssemblies(string runtimeIdentifier)
        {
            var assemblyNames = _dependencyContext.GetRuntimeAssemblyNames(runtimeIdentifier);

            var duplicateAssemblies = assemblyNames.GroupBy(n => n.Name).Where(g => g.Count() > 1);
            duplicateAssemblies.Select(g => g.Key).Should().BeEmpty();

            return new AndConstraint<DependencyContextAssertions>(this);
        }

        public AndConstraint<DependencyContextAssertions> HaveNoDuplicateNativeAssets(string runtimeIdentifier)
        {
            var nativeAssets = _dependencyContext.GetRuntimeNativeAssets(runtimeIdentifier);
            var nativeFilenames = nativeAssets.Select(n => Path.GetFileName(n));
            var duplicateNativeAssets = nativeFilenames.GroupBy(n => n).Where(g => g.Count() > 1);
            duplicateNativeAssets.Select(g => g.Key).Should().BeEmpty();

            return new AndConstraint<DependencyContextAssertions>(this);
        }

        public AndConstraint<DependencyContextAssertions> OnlyHaveRuntimeAssemblies(string runtimeIdentifier, params string[] runtimeAssemblyNames)
        {
            var assemblyNames = _dependencyContext.GetRuntimeAssemblyNames(runtimeIdentifier);

            assemblyNames.Select(n => n.Name)
                .Should()
                .BeEquivalentTo(runtimeAssemblyNames);

            return new AndConstraint<DependencyContextAssertions>(this);
        }

        public AndConstraint<DependencyContextAssertions> OnlyHaveRuntimeAssembliesWhichAreInFolder(string runtimeIdentifier, string folder)
        {
            var assemblyNames = _dependencyContext.GetRuntimeAssemblyNames(runtimeIdentifier);

            var assemblyFiles = assemblyNames.Select(an => Path.Combine(folder, an.Name + ".dll"));

            var missingFiles = assemblyFiles.Where(f => !File.Exists(f));

            missingFiles.Should().BeEmpty();

            return new AndConstraint<DependencyContextAssertions>(this);
        }

        public AndConstraint<DependencyContextAssertions> OnlyHaveNativeAssembliesWhichAreInFolder(string runtimeIdentifier, string folder, string appName)
        {
            var nativeAssets = _dependencyContext.GetRuntimeNativeAssets(runtimeIdentifier);
            var nativeAssetsWithPath = nativeAssets.Select(f =>
            {
                //  apphost gets renamed to the name of the app in self-contained publish
                if (Path.GetFileNameWithoutExtension(f) == "apphost")
                {
                    return Path.Combine(folder, appName + Constants.ExeSuffix);
                }
                else
                {
                    return Path.Combine(folder, Path.GetFileName(f));
                }
            });
            var missingNativeAssets = nativeAssetsWithPath.Where(f => !File.Exists(f));
            missingNativeAssets.Should().BeEmpty();

            return new AndConstraint<DependencyContextAssertions>(this);
        }

        public AndConstraint<DependencyContextAssertions> OnlyHavePackagesWithPathProperties()
        {
            var packageLibraries = _dependencyContext.RuntimeLibraries
                .Union<Library>(_dependencyContext.CompileLibraries)
                .Where(l => string.Equals(l.Type, "package", StringComparison.OrdinalIgnoreCase));

            foreach (var packageLibrary in packageLibraries)
            {
                packageLibrary.Path.Should().NotBeNullOrEmpty($"Every Library with Type='package' should have a Path, but {packageLibrary.Name} does not.");
                packageLibrary.HashPath.Should().NotBeNullOrEmpty($"Every Library with Type='package' should have a HashPath, but {packageLibrary.Name} does not.");
            }

            return new AndConstraint<DependencyContextAssertions>(this);
        }
    }
}
