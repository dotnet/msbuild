// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.PackageValidation
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string PackageTargetPath { get; set; }

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public string BaselinePackageTargetPath { get; set; }

        protected override void ExecuteCore()
        {
            RuntimeGraph runtimeGraph = null;
            if (!string.IsNullOrEmpty(RuntimeGraph))
            {
                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(RuntimeGraph);
            }

            Package package = NupkgParser.CreatePackage(PackageTargetPath, runtimeGraph);
            PackageValidationLogger logger = new(Log);

            new CompatibleTfmValidator(NoWarn, null, RunApiCompat, logger).Validate(package);
            new CompatibleFrameworkInPackageValidator(NoWarn, null, logger).Validate(package);
            if (!string.IsNullOrEmpty(BaselinePackageTargetPath))
            {
                Package baselinePackage = NupkgParser.CreatePackage(BaselinePackageTargetPath, runtimeGraph);
                new BaselinePackageValidator(baselinePackage, NoWarn, null, RunApiCompat, logger).Validate(package);
            }
        }
    }
}
