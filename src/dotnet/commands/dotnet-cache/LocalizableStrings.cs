// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Cache
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Cache";

        public const string AppDescription = "Caches the specified assemblies for the .NET Platform. By default, these will be optimized for the target runtime and framework.";

        public const string ProjectEntries = "ProjectEntries";

        public const string ProjectEntryDescription = "The XML file that contains the list of packages to be cached.";

        public const string FrameworkOption = "FRAMEWORK";

        public const string FrameworkOptionDescription = "Target framework for which to cache for.";

        public const string RuntimeOption = "RUNTIME_IDENTIFIER";

        public const string RuntimeOptionDescription = "Target runtime to cache for.";

        public const string OutputOption = "OUTPUT_DIR";

        public const string OutputOptionDescription = "Output directory in which to cache the given assemblies.";

        public const string FrameworkVersionOption = "FrameworkVersion";

        public const string FrameworkVersionOptionDescription = "The Microsoft.NETCore.App package version that will be used to run the assemblies.";

        public const string SkipOptimizationOptionDescription = "Skips the optimization phase.";

        public const string IntermediateWorkingDirOption = "IntermediateWorkingDir";

        public const string IntermediateWorkingDirOptionDescription = "The directory used by the command to execute.";

        public const string PreserveIntermediateWorkingDirOptionDescription = "Preserves the intermediate working directory.";

        public const string SpecifyEntries = "Specify at least one entry with --entries.";

        public const string IntermediateDirExists = "Intermediate working directory {0} already exists. Remove {0} or specify another directory with -w.";
    }
}
