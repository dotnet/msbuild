// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Add.PackageReference
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Add Package reference Command";

        public const string AppDescription = "Command to add package reference";

        public const string AppHelpText = "Package references to add";

        public const string CmdFrameworkDescription = "Add reference only when targetting a specific framework";

        public const string CmdNoRestoreDescription = "Add reference without performing restore preview and compatibility check.";

        public const string CmdSourceDescription = "Use specific NuGet package sources to use during the restore.";

        public const string CmdPackageDirectoryDescription = "Restore the packages to this Directory .";
    }
}