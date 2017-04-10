// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Store
{
    internal class LocalizableStrings
    {
        public const string AppDescription = "Stores the specified assemblies for the .NET Platform. By default, these will be optimized for the target runtime and framework.";

        public const string ProjectManifest = "PROJECT_MANIFEST";

        public const string ProjectManifestDescription = "The XML file that contains the list of packages to be stored.";

        public const string OutputOption = "OUTPUT_DIR";

        public const string OutputOptionDescription = "Output directory in which to store the given assemblies.";

        public const string FrameworkVersionOption = "FrameworkVersion";

        public const string FrameworkVersionOptionDescription = "The Microsoft.NETCore.App package version that will be used to run the assemblies.";

        public const string SkipOptimizationOptionDescription = "Skips the optimization phase.";

        public const string IntermediateWorkingDirOption = "IntermediateWorkingDir";

        public const string IntermediateWorkingDirOptionDescription = "The directory used by the command to execute.";

        public const string PreserveIntermediateWorkingDirOptionDescription = "Preserves the intermediate working directory.";

        public const string SpecifyManifests = "Specify at least one manifest with --manifest.";

        public const string IntermediateDirExists = "Intermediate working directory {0} already exists. Remove {0} or specify another directory with -w.";
    }
}
