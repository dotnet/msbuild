// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Build
{
    internal class LocalizableStrings
    {
        public const string AppDescription = "Builder for the .NET Platform. Delegates to the MSBuild 'Build' target in the project file.";

        public const string AppFullName = ".NET Builder";

        public const string NoDependenciesOptionDescription = "Set this flag to ignore project-to-project references and only build the root project";

        public const string NoIncrementialOptionDescription = "Disables incremental build.";

        public const string OutputOptionDescription = "Output directory in which to place built artifacts.";

        public const string OutputOptionName = "OUTPUT_DIR";
    }
}
