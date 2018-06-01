// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns the paths to the various frameworks versions.
    /// </summary>
    public class GetFrameworkPath : TaskExtension
    {
        static GetFrameworkPath()
        {
            s_path           = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Latest));
            s_version11Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11));
            s_version20Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20));
            s_version30Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30));
            s_version35Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35));
            s_version40Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40));
            s_version45Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45));
            s_version451Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version451));
            s_version452Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version452));
            s_version46Path  = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version46));
            s_version461Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version461));
            s_version462Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version462));
            s_version47Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version47));
            s_version471Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version471));
            s_version472Path = new Lazy<string>(() => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version472));
        }

        #region ITask Members

        /// <summary>
        /// Does nothing: getters do all the work
        /// </summary>
        public override bool Execute()
        {
            return true;
        }

        #endregion

        #region Properties

        // PERF NOTE: We cache these values in statics -- although the code we call does this too,
        // it still seems to give an advantage perhaps because there is one less string copy.
        // In a large build, this adds up.
        // PERF NOTE: We also only find paths we are actually asked for (via <Output> tags)

        private static readonly Lazy<string> s_path;
        private static readonly Lazy<string> s_version11Path;
        private static readonly Lazy<string> s_version20Path;
        private static readonly Lazy<string> s_version30Path;
        private static readonly Lazy<string> s_version35Path;
        private static readonly Lazy<string> s_version40Path;
        private static readonly Lazy<string> s_version45Path;
        private static readonly Lazy<string> s_version451Path;
        private static readonly Lazy<string> s_version452Path;
        private static readonly Lazy<string> s_version46Path;
        private static readonly Lazy<string> s_version461Path;
        private static readonly Lazy<string> s_version462Path;
        private static readonly Lazy<string> s_version47Path;
        private static readonly Lazy<string> s_version471Path;
        private static readonly Lazy<string> s_version472Path;

        /// <summary>
        /// Path to the latest framework, whatever version it happens to be
        /// </summary>
        [Output]
        public string Path => s_path.Value;

        /// <summary>
        /// Path to the v1.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion11Path => s_version11Path.Value;

        /// <summary>
        /// Path to the v2.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion20Path => s_version20Path.Value;

        /// <summary>
        /// Path to the v3.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion30Path => s_version30Path.Value;

        /// <summary>
        /// Path to the v3.5 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion35Path => s_version35Path.Value;

        /// <summary>
        /// Path to the v4.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion40Path => s_version40Path.Value;

        /// <summary>
        /// Path to the v4.5 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion45Path => s_version45Path.Value;

        /// <summary>
        /// Path to the v4.5.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion451Path => s_version451Path.Value;

        /// <summary>
        /// Path to the v4.5.2 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion452Path => s_version452Path.Value;

        /// <summary>
        /// Path to the v4.6 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion46Path => s_version46Path.Value;

        /// <summary>
        /// Path to the v4.6.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion461Path => s_version461Path.Value;

        /// <summary>
        /// Path to the v4.6.2 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion462Path => s_version462Path.Value;

        /// <summary>
        /// Path to the v4.7 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion47Path => s_version47Path.Value;

        /// <summary>
        /// Path to the v4.7.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion471Path => s_version471Path.Value;

        /// <summary>
        /// Path to the v4.7.2 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion472Path => s_version471Path.Value;

        #endregion
    }
}
