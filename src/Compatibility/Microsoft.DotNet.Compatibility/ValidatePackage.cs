// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.DotNet.PackageValidation;
using Microsoft.NET.Build.Tasks;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.Compatibility
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string PackageTargetPath { get; set; }

        [Required]
        public string RoslynAssembliesPath { get; set; }

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public bool EnableStrictModeForCompatibleTfms { get; set; }

        public bool EnableStrictModeForCompatibleFrameworksInPackage { get; set; }

        public string BaselinePackageTargetPath { get; set; }

        public bool DisablePackageBaselineValidation { get; set; }

        public bool GenerateCompatibilitySuppressionFile { get; set; }

        public string CompatibilitySuppressionFilePath { get; set; }

        public ITaskItem[] ReferencePaths { get; set; }

        public override bool Execute()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolverForRoslyn;
            try
            {
                return base.Execute();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= ResolverForRoslyn;
            }
        }

        protected override void ExecuteCore()
        {
            RuntimeGraph runtimeGraph = null;
            if (!string.IsNullOrEmpty(RuntimeGraph))
            {
                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(RuntimeGraph);
            }

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

                    if (!apiCompatReferences.TryGetValue(tfm, out HashSet<string> directories))
                    {
                        directories = new();
                        apiCompatReferences.Add(tfm, directories);
                    }

                    directories.Add(referencePath);
                }
            }

            Package package = NupkgParser.CreatePackage(PackageTargetPath, runtimeGraph);
            CompatibilityLogger logger = new(Log, CompatibilitySuppressionFilePath, GenerateCompatibilitySuppressionFile);

            new CompatibleTfmValidator(NoWarn, null, RunApiCompat, EnableStrictModeForCompatibleTfms, logger, apiCompatReferences).Validate(package);
            new CompatibleFrameworkInPackageValidator(NoWarn, null, EnableStrictModeForCompatibleFrameworksInPackage, logger, apiCompatReferences).Validate(package);

            if (!DisablePackageBaselineValidation && !string.IsNullOrEmpty(BaselinePackageTargetPath))
            {
                Package baselinePackage = NupkgParser.CreatePackage(BaselinePackageTargetPath, runtimeGraph);
                new BaselinePackageValidator(baselinePackage, NoWarn, null, RunApiCompat, logger, apiCompatReferences).Validate(package);
            }

            if (GenerateCompatibilitySuppressionFile)
            {
                logger.GenerateSuppressionsFile(CompatibilitySuppressionFilePath);
            }
        }

        private Assembly ResolverForRoslyn(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new(args.Name);
            if (name.Name == "Microsoft.CodeAnalysis" || name.Name == "Microsoft.CodeAnalysis.CSharp")
            {
                Assembly asm = Assembly.LoadFrom(Path.Combine(RoslynAssembliesPath, $"{name.Name}.dll"));
                Version version = asm.GetName().Version;
                if (version < name.Version)
                {
                    throw new Exception(string.Format(Resources.UpdateSdkVersion, version, name.Version));
                }
                return asm;
            }
            return null;
        }
    }
}
