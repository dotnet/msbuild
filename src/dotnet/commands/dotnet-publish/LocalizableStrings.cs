// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Publish
{
    internal class LocalizableStrings
    {
        public const string AppDescription = "Publisher for the .NET Platform";

        public const string FrameworkOption = "FRAMEWORK";

        public const string FrameworkOptionDescription = "Target framework to publish for. The target framework has to be specified in the project file.";

        public const string OutputOption = "OUTPUT_DIR";

        public const string OutputOptionDescription = "Output directory in which to place the published artifacts.";

        public const string ManifestOption = "manifest.xml";

        public const string ManifestOptionDescription = "The path to a target manifest file that contains the list of packages to be excluded from the publish step.";

        public const string SelfContainedOptionDescription = "Publish the .NET Core runtime with your application so the runtime doesn't need to be installed on the target machine. Defaults to 'true' if a runtime identifier is specified.";
    }
}
