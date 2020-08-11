// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Contains a list of the special (reserved) properties that are settable by MSBuild code only.
    /// </summary>
    /// <owner>RGoel</owner>
    internal static class ReservedPropertyNames
    {
        internal const string projectDirectory        = "MSBuildProjectDirectory";
        internal const string projectDirectoryNoRoot  = "MSBuildProjectDirectoryNoRoot";
        internal const string projectFile             = "MSBuildProjectFile";
        internal const string projectExtension        = "MSBuildProjectExtension";
        internal const string projectFullPath         = "MSBuildProjectFullPath";
        internal const string projectName             = "MSBuildProjectName";
        internal const string binPath                 = "MSBuildBinPath";
        internal const string projectDefaultTargets   = "MSBuildProjectDefaultTargets";
        internal const string extensionsPath          = "MSBuildExtensionsPath";
        internal const string extensionsPath32        = "MSBuildExtensionsPath32";
        internal const string toolsPath               = "MSBuildToolsPath";
        internal const string toolsVersion            = "MSBuildToolsVersion";
        internal const string startupDirectory        = "MSBuildStartupDirectory";
        internal const string buildNodeCount          = "MSBuildNodeCount";
        internal const string extensionsPathSuffix    = "MSBuild";
        internal const string programFiles32          = "MSBuildProgramFiles32";
        internal const string assemblyVersion         = "MSBuildAssemblyVersion";

        /// <summary>
        /// Indicates if the given property is a reserved property.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="property"></param>
        /// <returns>true, if specified property is reserved</returns>
        internal static bool IsReservedProperty(string property)
        {
            return

                    (String.Equals(property, projectDirectory, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, projectFile, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, projectExtension, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, projectFullPath, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, projectName, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, binPath, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, toolsPath, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, projectDefaultTargets, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, programFiles32, StringComparison.OrdinalIgnoreCase)) ||
                    (String.Equals(property, assemblyVersion, StringComparison.OrdinalIgnoreCase))
                // Intentionally do not include MSBuildExtensionsPath or MSBuildExtensionsPath32 in this list.  We need tasks to be able to override those.
                ;
        }
    }

    /// <summary>
    /// Constants used by the Engine
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// If no default tools version is specified in the config file or registry, we'll use 2.0.
        /// The engine will use its binpath for the matching toolset path.
        /// </summary>
        internal const string defaultToolsVersion = "2.0";

        /// <summary>
        /// The toolsversion we will fall back to as a last resort if the default one cannot be found, this fallback should be the most current toolsversion known
        /// </summary>
        internal const string defaultFallbackToolsVersion = "4.0";

        
        internal const string defaultSolutionWrapperProjectToolsVersion = "4.0";

        /// <summary>
        /// Current version of this MSBuild Engine assembly in the 
        /// form, e.g, "4.0"
        /// </summary>
        internal static string AssemblyVersion
        {
            get
            {
                return MSBuildConstants.CurrentToolsVersion;
            }
        }

        internal const string defaultTargetCacheName = "##DefaultTargets##";
        internal const string initialTargetCacheName = "##InitialTargets##";
        internal const string projectIdCacheName = "##ProjectId##";

        // Name of the environment variable that always points to 32-bit program files.
        internal const string programFilesx86 = "ProgramFiles(x86)";
    }

    /// <summary>
    /// Function related constants.
    /// </summary>
    /// <remarks>
    /// Placed here to avoid StyleCop error.
    /// </remarks>
    internal static class FunctionConstants
    {
        /// <summary>
        /// Static methods that are allowed in constants. Key = Type or Type::Method, Value = AssemblyQualifiedTypeName (where null = mscorlib)
        /// </summary>
        private static ConcurrentDictionary<string, Tuple<string, Type>> availableStaticMethods;

        /// <summary>
        /// The set of available static methods.
        /// NOTE: Do not allow methods here that could do "bad" things under any circumstances.
        /// These must be completely benign operations, as they run during project load, which must be safe in VS.
        /// Key = Type or Type::Method, Value = AssemblyQualifiedTypeName (where null = mscorlib)
        /// </summary>
        internal static IDictionary<string, Tuple<string, Type>> AvailableStaticMethods
        {
            get
            {
                // Initialize lazily, as many projects might not
                // even use functions
                if (availableStaticMethods == null)
                {
                    // Initialization is thread-safe
                    InitializeAvailableMethods();
                }

                return availableStaticMethods;
            }
        }

        /// <summary>
        /// Re-initialize.
        /// Unit tests need this when they enable "unsafe" methods -- which will then go in the collection,
        /// and mess up subsequent tests.
        /// </summary>
        internal static void Reset_ForUnitTestsOnly()
        {
            InitializeAvailableMethods();
        }

        /// <summary>
        /// Fill up the dictionary for first use
        /// </summary>
        private static void InitializeAvailableMethods()
        {
            availableStaticMethods = new ConcurrentDictionary<string, Tuple<string, Type>>(StringComparer.OrdinalIgnoreCase);

            // Pre declare our common type Tuples
            Tuple<string, Type> environmentType = new Tuple<string, Type>(null, typeof(System.Environment));
            Tuple<string, Type> directoryType = new Tuple<string, Type>(null, typeof(System.IO.Directory));
            Tuple<string, Type> fileType = new Tuple<string, Type>(null, typeof(System.IO.File));

            // Make specific static methods available (Assembly qualified type names are *NOT* supported, only null which means mscorlib):
            availableStaticMethods.TryAdd("System.Environment::CommandLine", environmentType);
            availableStaticMethods.TryAdd("System.Environment::ExpandEnvironmentVariables", environmentType);
            availableStaticMethods.TryAdd("System.Environment::GetEnvironmentVariable", environmentType);
            availableStaticMethods.TryAdd("System.Environment::GetEnvironmentVariables", environmentType);
            availableStaticMethods.TryAdd("System.Environment::GetFolderPath", environmentType);
            availableStaticMethods.TryAdd("System.Environment::GetLogicalDrives", environmentType);
            availableStaticMethods.TryAdd("System.Environment::Is64BitOperatingSystem", environmentType);
            availableStaticMethods.TryAdd("System.Environment::Is64BitProcess", environmentType);
            availableStaticMethods.TryAdd("System.Environment::MachineName", environmentType);
            availableStaticMethods.TryAdd("System.Environment::OSVersion", environmentType);
            availableStaticMethods.TryAdd("System.Environment::ProcessorCount", environmentType);
            availableStaticMethods.TryAdd("System.Environment::StackTrace", environmentType);
            availableStaticMethods.TryAdd("System.Environment::SystemDirectory", environmentType);
            availableStaticMethods.TryAdd("System.Environment::SystemPageSize", environmentType);
            availableStaticMethods.TryAdd("System.Environment::TickCount", environmentType);
            availableStaticMethods.TryAdd("System.Environment::UserDomainName", environmentType);
            availableStaticMethods.TryAdd("System.Environment::UserInteractive", environmentType);
            availableStaticMethods.TryAdd("System.Environment::UserName", environmentType);
            availableStaticMethods.TryAdd("System.Environment::Version", environmentType);
            availableStaticMethods.TryAdd("System.Environment::WorkingSet", environmentType);

            availableStaticMethods.TryAdd("System.IO.Directory::GetDirectories", directoryType);
            availableStaticMethods.TryAdd("System.IO.Directory::GetFiles", directoryType);
            availableStaticMethods.TryAdd("System.IO.Directory::GetLastAccessTime", directoryType);
            availableStaticMethods.TryAdd("System.IO.Directory::GetLastWriteTime", directoryType);
            availableStaticMethods.TryAdd("System.IO.Directory::GetParent", directoryType);
            availableStaticMethods.TryAdd("System.IO.File::Exists", fileType);
            availableStaticMethods.TryAdd("System.IO.File::GetCreationTime", fileType);
            availableStaticMethods.TryAdd("System.IO.File::GetAttributes", fileType);
            availableStaticMethods.TryAdd("System.IO.File::GetLastAccessTime", fileType);
            availableStaticMethods.TryAdd("System.IO.File::GetLastWriteTime", fileType);
            availableStaticMethods.TryAdd("System.IO.File::ReadAllText", fileType);

            availableStaticMethods.TryAdd("System.Globalization.CultureInfo::GetCultureInfo", new Tuple<string, Type>(null, typeof(System.Globalization.CultureInfo))); // user request
            availableStaticMethods.TryAdd("System.Globalization.CultureInfo::CurrentUICulture", new Tuple<string, Type>(null, typeof(System.Globalization.CultureInfo))); // user request

            // All static methods of the following are available (Assembly qualified type names are supported):
            availableStaticMethods.TryAdd("MSBuild", new Tuple<string, Type>(null, typeof(Microsoft.Build.BuildEngine.IntrinsicFunctions)));
            availableStaticMethods.TryAdd("System.Byte", new Tuple<string, Type>(null, typeof(System.Byte)));
            availableStaticMethods.TryAdd("System.Char", new Tuple<string, Type>(null, typeof(System.Char)));
            availableStaticMethods.TryAdd("System.Convert", new Tuple<string, Type>(null, typeof(System.Convert)));
            availableStaticMethods.TryAdd("System.DateTime", new Tuple<string, Type>(null, typeof(System.DateTime)));
            availableStaticMethods.TryAdd("System.Decimal", new Tuple<string, Type>(null, typeof(System.Decimal)));
            availableStaticMethods.TryAdd("System.Double", new Tuple<string, Type>(null, typeof(System.Double)));
            availableStaticMethods.TryAdd("System.Enum", new Tuple<string, Type>(null, typeof(System.Enum)));
            availableStaticMethods.TryAdd("System.Guid", new Tuple<string, Type>(null, typeof(System.Guid)));
            availableStaticMethods.TryAdd("System.Int16", new Tuple<string, Type>(null, typeof(System.Int16)));
            availableStaticMethods.TryAdd("System.Int32", new Tuple<string, Type>(null, typeof(System.Int32)));
            availableStaticMethods.TryAdd("System.Int64", new Tuple<string, Type>(null, typeof(System.Int64)));
            availableStaticMethods.TryAdd("System.IO.Path", new Tuple<string, Type>(null, typeof(System.IO.Path)));
            availableStaticMethods.TryAdd("System.Math", new Tuple<string, Type>(null, typeof(System.Math)));
            availableStaticMethods.TryAdd("System.UInt16", new Tuple<string, Type>(null, typeof(System.UInt16)));
            availableStaticMethods.TryAdd("System.UInt32", new Tuple<string, Type>(null, typeof(System.UInt32)));
            availableStaticMethods.TryAdd("System.UInt64", new Tuple<string, Type>(null, typeof(System.UInt64)));
            availableStaticMethods.TryAdd("System.SByte", new Tuple<string, Type>(null, typeof(System.SByte)));
            availableStaticMethods.TryAdd("System.Single", new Tuple<string, Type>(null, typeof(System.Single)));
            availableStaticMethods.TryAdd("System.String", new Tuple<string, Type>(null, typeof(System.String)));
            availableStaticMethods.TryAdd("System.StringComparer", new Tuple<string, Type>(null, typeof(System.StringComparer)));
            availableStaticMethods.TryAdd("System.TimeSpan", new Tuple<string, Type>(null, typeof(System.TimeSpan)));
            availableStaticMethods.TryAdd("System.Text.RegularExpressions.Regex", new Tuple<string, Type>(null, typeof(System.Text.RegularExpressions.Regex)));
            availableStaticMethods.TryAdd("System.UriBuilder", new Tuple<string, Type>(null, typeof(System.UriBuilder)));
            availableStaticMethods.TryAdd("System.Version", new Tuple<string, Type>(null, typeof(System.Version)));
            availableStaticMethods.TryAdd("Microsoft.Build.Utilities.ToolLocationHelper", new Tuple<string, Type>("Microsoft.Build.Utilities.ToolLocationHelper, Microsoft.Build.Utilities.Core, Version=" + MSBuildConstants.CurrentAssemblyVersion + ", Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", null));
        }
    }
}
