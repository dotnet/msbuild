// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Constants used for assembly resolution.
    /// </summary>
    internal static class AssemblyResolutionConstants
    {
        /// <summary>
        /// Special hintpath indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string hintPathSentinel = "{hintpathfromitem}";

        /// <summary>
        /// Special AssemblyFolders indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string assemblyFoldersSentinel = "{assemblyfolders}";

        /// <summary>
        /// Special CandidateAssemblyFiles indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string candidateAssemblyFilesSentinel = "{candidateassemblyfiles}";

        /// <summary>
        /// Special GAC indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string gacSentinel = "{gac}";

        /// <summary>
        /// Special Framework directory indicator. May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string frameworkPathSentinel = "{targetframeworkdirectory}";

        /// <summary>
        /// Special SearchPath indicator that means: match against the assembly item's Include as
        /// if it were a file.
        /// </summary>
        public const string rawFileNameSentinel = "{rawfilename}";

        /// <summary>
        /// Special AssemblyFoldersEx indicator.  May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string assemblyFoldersExSentinel = "{registry:";

        /// <summary>
        /// Special AssemblyFoldersFromConfig indicator.  May be passed in where SearchPaths are taken. 
        /// </summary>
        public const string assemblyFoldersFromConfigSentinel = "{assemblyfoldersfromconfig:";
    }
}
