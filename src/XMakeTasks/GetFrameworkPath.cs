// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Resources;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns the paths to the various frameworks versions.
    /// </summary>
    public class GetFrameworkPath : TaskExtension
    {
        #region Properties

        // PERF NOTE: We cache these values in statics -- although the code we call does this too,
        // it still seems to give an advantage perhaps because there is one less string copy.
        // In a large build, this adds up.
        // PERF NOTE: We also only find paths we are actually asked for (via <Output> tags)

        private static string s_path;
        private static string s_version11Path;
        private static string s_version20Path;
        private static string s_version30Path;
        private static string s_version35Path;
        private static string s_version40Path;
        private static string s_version45Path;
        private static string s_version451Path;
        private static string s_version46Path;
        private static string s_version461Path;

        /// <summary>
        /// Path to the latest framework, whatever version it happens to be
        /// </summary>
        [Output]
        public string Path
        {
            get
            {
                if (s_path == null)
                {
                    s_path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest);
                }

                return s_path;
            }

            set
            {
                // Does nothing: backward compat
                s_path = value;
            }
        }

        /// <summary>
        /// Path to the v1.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion11Path
        {
            get
            {
                if (s_version11Path == null)
                {
                    s_version11Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11);
                }

                return s_version11Path;
            }
        }

        /// <summary>
        /// Path to the v2.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion20Path
        {
            get
            {
                if (s_version20Path == null)
                {
                    s_version20Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20);
                }

                return s_version20Path;
            }
        }

        /// <summary>
        /// Path to the v3.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion30Path
        {
            get
            {
                if (s_version30Path == null)
                {
                    s_version30Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30);
                }

                return s_version30Path;
            }
        }

        /// <summary>
        /// Path to the v3.5 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion35Path
        {
            get
            {
                if (s_version35Path == null)
                {
                    s_version35Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35);
                }

                return s_version35Path;
            }
        }

        /// <summary>
        /// Path to the v4.0 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion40Path
        {
            get
            {
                if (s_version40Path == null)
                {
                    s_version40Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40);
                }

                return s_version40Path;
            }
        }

        /// <summary>
        /// Path to the v4.5 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion45Path
        {
            get
            {
                if (s_version45Path == null)
                {
                    s_version45Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45);
                }

                return s_version45Path;
            }
        }

        /// <summary>
        /// Path to the v4.5.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion451Path
        {
            get
            {
                if (s_version451Path == null)
                {
                    s_version451Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version451);
                }

                return s_version451Path;
            }
        }

        /// <summary>
        /// Path to the v4.6 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion46Path
        {
            get
            {
                if (s_version46Path == null)
                {
                    s_version46Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version46);
                }

                return s_version46Path;
            }
        }

        /// <summary>
        /// Path to the v4.6.1 framework, if available
        /// </summary>
        [Output]
        public string FrameworkVersion461Path
        {
            get
            {
                if (s_version461Path == null)
                {
                    s_version461Path = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version461);
                }

                return s_version461Path;
            }
        }

        #endregion

        #region ITask Members

        /// <summary>
        /// Does nothing: getters do all the work
        /// </summary>
        public override bool Execute()
        {
            return true;
        }

        #endregion
    }
}
