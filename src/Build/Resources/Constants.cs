// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Contains a list of the special (reserved) properties that are settable by MSBuild code only.
    /// </summary>
    internal static class ReservedPropertyNames
    {
        // NOTE: if you add to this list, update the ReservedProperties hashtable below
        internal const string projectDirectory = "MSBuildProjectDirectory";
        internal const string projectDirectoryNoRoot = "MSBuildProjectDirectoryNoRoot";
        internal const string projectFile = "MSBuildProjectFile";
        internal const string projectExtension = "MSBuildProjectExtension";
        internal const string projectFullPath = "MSBuildProjectFullPath";
        internal const string projectName = "MSBuildProjectName";
        internal const string thisFileDirectory = "MSBuildThisFileDirectory";
        internal const string thisFileDirectoryNoRoot = "MSBuildThisFileDirectoryNoRoot";
        internal const string thisFile = "MSBuildThisFile"; // "MSBuildThisFileFile" sounds silly!
        internal const string thisFileExtension = "MSBuildThisFileExtension";
        internal const string thisFileFullPath = "MSBuildThisFileFullPath";
        internal const string thisFileName = "MSBuildThisFileName";
        internal const string binPath = "MSBuildBinPath";
        internal const string projectDefaultTargets = "MSBuildProjectDefaultTargets";
        internal const string extensionsPath = "MSBuildExtensionsPath";
        internal const string extensionsPath32 = "MSBuildExtensionsPath32";
        internal const string extensionsPath64 = "MSBuildExtensionsPath64";
        internal const string userExtensionsPath = "MSBuildUserExtensionsPath";
        internal const string toolsPath = MSBuildConstants.ToolsPath;
        internal const string toolsVersion = "MSBuildToolsVersion";
        internal const string msbuildRuntimeType = "MSBuildRuntimeType";
        internal const string overrideTasksPath = "MSBuildOverrideTasksPath";
        internal const string defaultOverrideToolsVersion = "DefaultOverrideToolsVersion";
        internal const string startupDirectory = "MSBuildStartupDirectory";
        internal const string buildNodeCount = "MSBuildNodeCount";
        internal const string lastTaskResult = "MSBuildLastTaskResult";
        internal const string extensionsPathSuffix = "MSBuild";
        internal const string userExtensionsPathSuffix = "Microsoft\\MSBuild";
        internal const string programFiles32 = "MSBuildProgramFiles32";
        internal const string localAppData = "LocalAppData";
        internal const string assemblyVersion = "MSBuildAssemblyVersion";
        internal const string osName = "OS";
        internal const string frameworkToolsRoot = "MSBuildFrameworkToolsRoot";

        /// <summary>
        /// Lookup for reserved property names
        /// </summary>
        private static HashSet<string> s_reservedProperties;

        /// <summary>
        /// Lock object, since this is a shared table, and concurrent evaluation must be safe
        /// </summary>
        private static Object s_locker = new Object();

        /// <summary>
        /// Intentionally do not include MSBuildExtensionsPath* or MSBuildUserExtensionsPath in this list.  We need tasks to be able to override those.
        /// </summary>
        private static HashSet<string> ReservedProperties
        {
            get
            {
                lock (s_locker)
                {
                    if (s_reservedProperties == null)
                    {
                        s_reservedProperties = new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);

                        s_reservedProperties.Add(projectDirectory);
                        s_reservedProperties.Add(projectDirectoryNoRoot);
                        s_reservedProperties.Add(projectFile);
                        s_reservedProperties.Add(projectExtension);
                        s_reservedProperties.Add(projectFullPath);
                        s_reservedProperties.Add(projectName);

                        s_reservedProperties.Add(thisFileDirectory);
                        s_reservedProperties.Add(thisFileDirectoryNoRoot);
                        s_reservedProperties.Add(thisFile);
                        s_reservedProperties.Add(thisFileExtension);
                        s_reservedProperties.Add(thisFileFullPath);
                        s_reservedProperties.Add(thisFileName);

                        s_reservedProperties.Add(binPath);
                        s_reservedProperties.Add(projectDefaultTargets);
                        s_reservedProperties.Add(toolsPath);
                        s_reservedProperties.Add(toolsVersion);
                        s_reservedProperties.Add(msbuildRuntimeType);
                        s_reservedProperties.Add(startupDirectory);
                        s_reservedProperties.Add(buildNodeCount);
                        s_reservedProperties.Add(lastTaskResult);
                        s_reservedProperties.Add(programFiles32);
                        s_reservedProperties.Add(assemblyVersion);
                    }
                }

                return s_reservedProperties;
            }
        }

        /// <summary>
        /// Indicates if the given property is a reserved property.
        /// </summary>
        /// <returns>true, if specified property is reserved</returns>
        internal static bool IsReservedProperty(string property)
        {
            return ReservedProperties.Contains(property);
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
        internal static string defaultFallbackToolsVersion = MSBuildConstants.CurrentToolsVersion;

        /// <summary>
        /// The toolsversion we will use when we construct the solution wrapper metaprojects; this should be the most current toolsversion known
        /// </summary>
        internal static string defaultSolutionWrapperProjectToolsVersion = MSBuildConstants.CurrentToolsVersion;

        /// <summary>
        /// Name of the property used to select which sub-toolset to use. 
        /// </summary>
        internal const string SubToolsetVersionPropertyName = "VisualStudioVersion";

        /// <summary>
        /// Value we should be setting VisualStudioVersion as the ultimate fallback when Dev10 is installed. 
        /// </summary>
        internal const string Dev10SubToolsetValue = "10.0";

        /// <summary>
        /// Number representing the current assembly's timestamp
        /// </summary>
        internal static long assemblyTimestamp;

        /// <summary>
        /// Current version of this MSBuild Engine assembly in the 
        /// form, e.g, "4.0"
        /// </summary>
        internal static string AssemblyVersion
        {
            get
            {
#if STANDALONEBUILD
                return MSBuildConstants.CurrentToolsVersion;
#else
                Version version = new Version(ThisAssembly.Version);

                // "4.0.0.0" --> "4.0"
                return version.Major + "." + version.Minor;
#endif
            }
        }


        /// <summary>
        /// Number representing the current assembly's timestamp
        /// </summary>
        internal static long AssemblyTimestamp
        {
            get
            {
                if (assemblyTimestamp == 0)
                {
                    // Get the file version from the currently executing assembly.
                    // Use .CodeBase instead of .Location, because .Location doesn't
                    // work when Microsoft.Build.dll has been shadow-copied, for example
                    // in scenarios where NUnit is loading Microsoft.Build.
                    string path = FileUtilities.ExecutingAssemblyPath;

                    assemblyTimestamp = new FileInfo(path).LastWriteTime.Ticks;
                }

                return assemblyTimestamp;
            }
        }

        // Name of the environment variable that always points to 32-bit program files.
        internal const string programFilesx86 = "ProgramFiles(x86)";
    }

    /// <summary>
    /// The set of available static methods.
    /// NOTE: Do not allow methods here that could do "bad" things under any circumstances.
    /// These must be completely benign operations, as they run during project load, which must be safe in VS.
    /// Key = Type or Type::Method, Value = AssemblyQualifiedTypeName (where null = mscorlib)
    /// </summary>
    /// <remarks>
    /// Placed here to avoid StyleCop error.
    /// </remarks>
    internal static class AvailableStaticMethods
    {
        /// <summary>
        /// Static methods that are allowed in constants. Key = Type or Type::Method, Value = Tuple of AssemblyQualifiedTypeName (where null = mscorlib) or the actual type object
        /// </summary>
        private static ConcurrentDictionary<string, Tuple<string, Type>> s_availableStaticMethods;

        /// <summary>
        /// Locker to protect initialization
        /// </summary>
        private static Object s_locker = new Object();

        static AvailableStaticMethods()
        {
            InitializeAvailableMethods();
        }

        /// <summary>
        /// Whether a key is present
        /// </summary>
        internal static bool ContainsKey(string key)
        {
            return s_availableStaticMethods.ContainsKey(key);
        }
        /// <summary>
        /// Add an entry if not already present
        /// </summary>
        internal static bool TryAdd(string key, Tuple<string, Type> value)
        {
            return s_availableStaticMethods.TryAdd(key, value);
        }

        /// <summary>
        /// Constructs the fully qualified method name and adds it to the cache
        /// </summary>
        /// <param name="typeFullName"></param>
        /// <param name="simpleMethodName"></param>
        /// <param name="typeInformation"></param>
        /// <returns></returns>
        public static bool TryAdd(string typeFullName, string simpleMethodName, Tuple<string, Type> typeInformation)
        {
            return TryAdd(CreateQualifiedMethodName(typeFullName, simpleMethodName), typeInformation);
        }

        /// <summary>
        /// Get an entry if present
        /// </summary>
        internal static bool TryGetValue(string key, out Tuple<string, Type> value)
        {
            return s_availableStaticMethods.TryGetValue(key, out value);
        }

        /// <summary>
        /// Get an entry or null if not present
        /// </summary>
        internal static Tuple<string, Type> GetValue(string key)
        {
            Tuple<string, Type> typeInformation;
            return s_availableStaticMethods.TryGetValue(key, out typeInformation) ? typeInformation : null;
        }

        /// <summary>
        /// Tries to retrieve the type information for a type name / method name combination. 
        /// 
        /// It does 2 lookups:
        /// 1st try: 'typeFullName'
        /// 2nd try: 'typeFullName::simpleMethodName'
        /// 
        /// </summary>
        /// <param name="typeFullName">namespace qualified type name</param>
        /// <param name="simpleMethodName">name of the method</param>
        /// <returns></returns>
        internal static Tuple<string, Type> GetTypeInformationFromTypeCache(string typeFullName, string simpleMethodName)
        {
            return
                GetValue(typeFullName) ??
                GetValue(CreateQualifiedMethodName(typeFullName, simpleMethodName));
        }

        /// <summary>
        /// Returns the fully qualified format for the given method: see typeFullName::methodName
        /// </summary>
        /// <param name="typeFullName">namespace qualified type name</param>
        /// <param name="simpleMethodName">simple name of the method</param>
        /// <returns></returns>
        private static string CreateQualifiedMethodName(string typeFullName, string simpleMethodName)
        {
            return $"{typeFullName}::{simpleMethodName}";
        }


        /// <summary>
        /// Re-initialize.
        /// Unit tests need this when they enable "unsafe" methods -- which will then go in the collection,
        /// and mess up subsequent tests.
        /// </summary>
        internal static void Reset_ForUnitTestsOnly()
        {
            lock (s_locker)
            {
                s_availableStaticMethods = null;
                InitializeAvailableMethods();
            }
        }

        /// <summary>
        /// Fill up the dictionary for first use
        /// </summary>
        private static void InitializeAvailableMethods()
        {
            lock (s_locker)
            {
                if (s_availableStaticMethods == null)
                {
                    s_availableStaticMethods = new ConcurrentDictionary<string, Tuple<string, Type>>(StringComparer.OrdinalIgnoreCase);

                    // Pre declare our common type Tuples
                    var environmentType = new Tuple<string, Type>(null, typeof(System.Environment));
                    var directoryType = new Tuple<string, Type>(null, typeof(System.IO.Directory));
                    var fileType = new Tuple<string, Type>(null, typeof(System.IO.File));
#if FEATURE_RUNTIMEINFORMATION
                    var runtimeInformationType = new Tuple<string, Type>(null, typeof(System.Runtime.InteropServices.RuntimeInformation));
                    var osPlatformType = new Tuple<string, Type>(null, typeof(System.Runtime.InteropServices.OSPlatform));
#endif

                    // Make specific static methods available (Assembly qualified type names are *NOT* supported, only null which means mscorlib):
                    TryAdd("System.Environment::ExpandEnvironmentVariables", environmentType);
                    TryAdd("System.Environment::GetEnvironmentVariable", environmentType);
                    TryAdd("System.Environment::GetEnvironmentVariables", environmentType);
#if FEATURE_SPECIAL_FOLDERS
                    TryAdd("System.Environment::GetFolderPath", environmentType);
                    TryAdd("System.Environment::GetLogicalDrives", environmentType);
#endif

// All the following properties only have getters
#if FEATURE_GET_COMMANDLINE
                    TryAdd("System.Environment::CommandLine", environmentType);
#endif
#if FEATURE_64BIT_ENVIRONMENT_QUERY
                    TryAdd("System.Environment::Is64BitOperatingSystem", environmentType);
                    TryAdd("System.Environment::Is64BitProcess", environmentType);
#endif

                    TryAdd("System.Environment::MachineName", environmentType);
#if FEATURE_OSVERSION
                    TryAdd("System.Environment::OSVersion", environmentType);
#endif
                    TryAdd("System.Environment::ProcessorCount", environmentType);
                    TryAdd("System.Environment::StackTrace", environmentType);
#if FEATURE_SPECIAL_FOLDERS
                    TryAdd("System.Environment::SystemDirectory", environmentType);
#endif
#if FEATURE_SYSTEMPAGESIZE
                    TryAdd("System.Environment::SystemPageSize", environmentType);
#endif
                    TryAdd("System.Environment::TickCount", environmentType);
#if FEATURE_USERDOMAINNAME
                    TryAdd("System.Environment::UserDomainName", environmentType);
#endif
#if FEATURE_USERINTERACTIVE
                    TryAdd("System.Environment::UserInteractive", environmentType);
#endif
                    TryAdd("System.Environment::UserName", environmentType);
#if FEATURE_DOTNETVERSION
                    TryAdd("System.Environment::Version", environmentType);
#endif
#if FEATURE_WORKINGSET
                    TryAdd("System.Environment::WorkingSet", environmentType);
#endif

                    TryAdd("System.IO.Directory::GetDirectories", directoryType);
                    TryAdd("System.IO.Directory::GetFiles", directoryType);
                    TryAdd("System.IO.Directory::GetLastAccessTime", directoryType);
                    TryAdd("System.IO.Directory::GetLastWriteTime", directoryType);
                    TryAdd("System.IO.Directory::GetParent", directoryType);
                    TryAdd("System.IO.File::Exists", fileType);
                    TryAdd("System.IO.File::GetCreationTime", fileType);
                    TryAdd("System.IO.File::GetAttributes", fileType);
                    TryAdd("System.IO.File::GetLastAccessTime", fileType);
                    TryAdd("System.IO.File::GetLastWriteTime", fileType);
                    TryAdd("System.IO.File::ReadAllText", fileType);

#if FEATURE_CULTUREINFO_GETCULTUREINFO
                    TryAdd("System.Globalization.CultureInfo::GetCultureInfo", new Tuple<string, Type>(null, typeof(System.Globalization.CultureInfo))); // user request
#endif
                    TryAdd("System.Globalization.CultureInfo::new", new Tuple<string, Type>(null, typeof(System.Globalization.CultureInfo))); // user request
                    TryAdd("System.Globalization.CultureInfo::CurrentUICulture", new Tuple<string, Type>(null, typeof(System.Globalization.CultureInfo))); // user request

                    // All static methods of the following are available (Assembly qualified type names are supported):
                    TryAdd("MSBuild", new Tuple<string, Type>(null, typeof(Microsoft.Build.Evaluation.IntrinsicFunctions)));
                    TryAdd("System.Byte", new Tuple<string, Type>(null, typeof(System.Byte)));
                    TryAdd("System.Char", new Tuple<string, Type>(null, typeof(System.Char)));
                    TryAdd("System.Convert", new Tuple<string, Type>(null, typeof(System.Convert)));
                    TryAdd("System.DateTime", new Tuple<string, Type>(null, typeof(System.DateTime)));
                    TryAdd("System.Decimal", new Tuple<string, Type>(null, typeof(System.Decimal)));
                    TryAdd("System.Double", new Tuple<string, Type>(null, typeof(System.Double)));
                    TryAdd("System.Enum", new Tuple<string, Type>(null, typeof(System.Enum)));
                    TryAdd("System.Guid", new Tuple<string, Type>(null, typeof(System.Guid)));
                    TryAdd("System.Int16", new Tuple<string, Type>(null, typeof(System.Int16)));
                    TryAdd("System.Int32", new Tuple<string, Type>(null, typeof(System.Int32)));
                    TryAdd("System.Int64", new Tuple<string, Type>(null, typeof(System.Int64)));
                    TryAdd("System.IO.Path", new Tuple<string, Type>(null, typeof(System.IO.Path)));
                    TryAdd("System.Math", new Tuple<string, Type>(null, typeof(System.Math)));
                    TryAdd("System.UInt16", new Tuple<string, Type>(null, typeof(System.UInt16)));
                    TryAdd("System.UInt32", new Tuple<string, Type>(null, typeof(System.UInt32)));
                    TryAdd("System.UInt64", new Tuple<string, Type>(null, typeof(System.UInt64)));
                    TryAdd("System.SByte", new Tuple<string, Type>(null, typeof(System.SByte)));
                    TryAdd("System.Single", new Tuple<string, Type>(null, typeof(System.Single)));
                    TryAdd("System.String", new Tuple<string, Type>(null, typeof(System.String)));
                    TryAdd("System.StringComparer", new Tuple<string, Type>(null, typeof(System.StringComparer)));
                    TryAdd("System.TimeSpan", new Tuple<string, Type>(null, typeof(System.TimeSpan)));
                    TryAdd("System.Text.RegularExpressions.Regex", new Tuple<string, Type>(null, typeof(System.Text.RegularExpressions.Regex)));
                    TryAdd("System.UriBuilder", new Tuple<string, Type>(null, typeof(System.UriBuilder)));
                    TryAdd("System.Version", new Tuple<string, Type>(null, typeof(System.Version)));
                    TryAdd("Microsoft.Build.Utilities.ToolLocationHelper", new Tuple<string, Type>("Microsoft.Build.Utilities.ToolLocationHelper, Microsoft.Build.Utilities.Core, Version=" + MSBuildConstants.CurrentAssemblyVersion + ", Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", null));
#if FEATURE_RUNTIMEINFORMATION
                    TryAdd("System.Runtime.InteropServices.RuntimeInformation", runtimeInformationType);
                    TryAdd("System.Runtime.InteropServices.OSPlatform", osPlatformType);
#endif
                }
            }
        }
    }
}
