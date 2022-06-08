// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace Microsoft.DotNet.Compatibility
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string? PackageTargetPath { get; set; }

        [Required]
        public string? RoslynAssembliesPath { get; set; }

        public string? RuntimeGraph { get; set; }

        public string? NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public bool EnableStrictModeForCompatibleTfms { get; set; }

        public bool EnableStrictModeForCompatibleFrameworksInPackage { get; set; }

        public string? BaselinePackageTargetPath { get; set; }

        public bool DisablePackageBaselineValidation { get; set; }

        public bool GenerateCompatibilitySuppressionFile { get; set; }

        public string? CompatibilitySuppressionFilePath { get; set; }

        public ITaskItem[]? ReferencePaths { get; set; }

        public override bool Execute()
        {
#if NETCOREAPP
            AssemblyLoadContext currentContext = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())!;
            currentContext.Resolving += ResolverForRoslyn;
#else
            AppDomain.CurrentDomain.AssemblyResolve += ResolverForRoslyn;
#endif
            try
            {
                return base.Execute();
            }
            finally
            {
#if NETCOREAPP
                currentContext.Resolving -= ResolverForRoslyn;
#else
                AppDomain.CurrentDomain.AssemblyResolve -= ResolverForRoslyn;
#endif
            }
        }

        protected override void ExecuteCore()
        {
            Dictionary<string, HashSet<string>> apiCompatReferences = new();
            if (ReferencePaths != null)
            {
                foreach (ITaskItem taskItem in ReferencePaths)
                {
                    string tfm = taskItem.GetMetadata("TargetFramework");
                    if (string.IsNullOrEmpty(tfm))
                        continue;

                    string referencePath = taskItem.GetMetadata("Identity");
                    if (!File.Exists(referencePath))
                        continue;

                    if (!apiCompatReferences.TryGetValue(tfm, out HashSet<string>? directories))
                    {
                        directories = new();
                        apiCompatReferences.Add(tfm, directories);
                    }

                    directories.Add(referencePath);
                }
            }

            MSBuildCompatibilityLogger log = new(Log, CompatibilitySuppressionFilePath, GenerateCompatibilitySuppressionFile, NoWarn);
            ValidatorBootstrap.RunValidators(log,
                PackageTargetPath!,
                GenerateCompatibilitySuppressionFile,
                RunApiCompat,
                EnableStrictModeForCompatibleTfms,
                EnableStrictModeForCompatibleFrameworksInPackage,
                DisablePackageBaselineValidation,
                BaselinePackageTargetPath,
                RuntimeGraph,
                apiCompatReferences);
        }

#if NETCOREAPP
        private Assembly? ResolverForRoslyn(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            return LoadRoslyn(assemblyName, path => context.LoadFromAssemblyPath(path));
        }
#else
        private Assembly? ResolverForRoslyn(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new(args.Name);
            return LoadRoslyn(name, path => Assembly.LoadFrom(path));
        }
#endif

        private Assembly? LoadRoslyn(AssemblyName name, Func<string, Assembly> loadFromPath)
        {
            const string codeAnalysisName = "Microsoft.CodeAnalysis";
            const string codeAnalysisCsharpName = "Microsoft.CodeAnalysis.CSharp";
            if (name.Name == codeAnalysisName || name.Name == codeAnalysisCsharpName)
            {
                Assembly asm = loadFromPath(Path.Combine(RoslynAssembliesPath!, $"{name.Name}.dll"));
                Version? resolvedVersion = asm.GetName().Version;
                if (resolvedVersion < name.Version)
                {
                    throw new Exception(string.Format(Resources.UpdateSdkVersion, resolvedVersion, name.Version));
                }

                // Being extra defensive but we want to avoid that we accidentally load two different versions of either
                // of the roslyn assemblies from a different location, so let's load them both on the first request.
                Assembly _ = name.Name == codeAnalysisName ?
                    loadFromPath(Path.Combine(RoslynAssembliesPath!, $"{codeAnalysisCsharpName}.dll")) :
                    loadFromPath(Path.Combine(RoslynAssembliesPath!, $"{codeAnalysisName}.dll"));

                return asm;
            }

            return null;
        }
    }
}
