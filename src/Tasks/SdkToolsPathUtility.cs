// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Provide a helper class for tasks to find their tools if they are in the SDK</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class will provide the ability for classes given an SdkToolsPath and their tool name to find that tool. 
    /// The tool will be looked for either under the SDKToolPath passed into the task or as fallback to look for the toolname using the toolslocation helper. 
    /// </summary>
    internal static class SdkToolsPathUtility
    {
        /// <summary>
        /// Cache the file exists delegate which will determine if a file exists or not but will not eat the CAS exceptions.
        /// </summary>
        private static FileExists s_fileInfoExists;

        /// <summary>
        /// Provide a delegate which will do the correct file exists.
        /// </summary>
        internal static FileExists FileInfoExists
        {
            get
            {
                if (s_fileInfoExists == null)
                {
                    s_fileInfoExists = FileExists;
                }

                return s_fileInfoExists;
            }
        }

        /// <summary>
        /// This method will take a sdkToolsPath and a toolName and return the path to the tool if it is found and exists.
        /// 
        /// First the method will try and find the tool under the sdkToolsPath taking into account the current processor architecture
        /// If the tool could not be found the method will try and find the tool under the sdkToolsPath (which should point to the x86 sdk directory).
        /// 
        /// Finally if the method has not found the tool yet it will fallback and use the toolslocation helper method to try and find the tool.
        /// </summary>
        /// <returns>Path including the toolName of the tool if found, null if it is not found</returns>
        internal static string GeneratePathToTool(FileExists fileExists, string currentArchitecture, string sdkToolsPath, string toolName, TaskLoggingHelper log, bool logErrorsAndWarnings)
        {
            // Null until we combine the toolname with the path.
            string pathToTool = null;
            if (!String.IsNullOrEmpty(sdkToolsPath))
            {
                string processorSpecificToolDirectory;
                try
                {
                    switch (currentArchitecture)
                    {
                        // There may not be an arm directory so we will fall back to the x86 tool location
                        // but if there is then we should try and use it.
                        case ProcessorArchitecture.ARM:
                            processorSpecificToolDirectory = Path.Combine(sdkToolsPath, "arm");
                            break;
                        case ProcessorArchitecture.AMD64:
                            processorSpecificToolDirectory = Path.Combine(sdkToolsPath, "x64");
                            break;
                        case ProcessorArchitecture.IA64:
                            processorSpecificToolDirectory = Path.Combine(sdkToolsPath, "ia64");
                            break;
                        case ProcessorArchitecture.X86:
                        default:
                            processorSpecificToolDirectory = sdkToolsPath;
                            break;
                    }

                    pathToTool = Path.Combine(processorSpecificToolDirectory, toolName);

                    if (!fileExists(pathToTool))
                    {
                        // Try falling back to the x86 location
                        if (currentArchitecture != ProcessorArchitecture.X86)
                        {
                            pathToTool = Path.Combine(sdkToolsPath, toolName);
                        }
                    }
                    else
                    {
                        return pathToTool;
                    }
                }
                catch (ArgumentException e)
                {
                    // Catch exceptions from path.combine
                    log.LogErrorWithCodeFromResources("General.SdkToolsPathError", toolName, e.Message);
                    return null;
                }

                if (fileExists(pathToTool))
                {
                    return pathToTool;
                }
                else
                {
                    if (logErrorsAndWarnings)
                    {
                        // Log an error indicating we could not find it in the processor specific architecture or x86 locations.
                        // We could not find the tool at all, lot a error.
                        log.LogWarningWithCodeFromResources("General.PlatformSDKFileNotFoundSdkToolsPath", toolName, processorSpecificToolDirectory, sdkToolsPath);
                    }
                }
            }
            else
            {
                if (logErrorsAndWarnings)
                {
                    log.LogMessageFromResources(MessageImportance.Low, "General.SdkToolsPathNotSpecifiedOrToolDoesNotExist", toolName, sdkToolsPath);
                }
            }

            // Fall back and see if we can find it with the toolsLocation helper methods. This is not optimal because 
            // the location they are looking at is based on when the Microsoft.Build.Utilities.dll was compiled
            // but it is better than nothing.
            if (null == pathToTool || !fileExists(pathToTool))
            {
                pathToTool = FindSDKToolUsingToolsLocationHelper(toolName);

                if (pathToTool == null && logErrorsAndWarnings)
                {
                    log.LogErrorWithCodeFromResources("General.SdkToolsPathToolDoesNotExist", toolName, sdkToolsPath, ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest));
                }
            }

            return pathToTool;
        }

        /// <summary>
        /// This method will take the toolName and use the Legacy ToolLocation helper methods to try and find the tool.
        /// This is a last ditch effort to find the tool when we cannot find it using the passed in SDKToolsPath (in either the x86 or processor specific directories).
        /// </summary>
        /// <param name="toolName">Name of the tool to find the sdk path for</param>
        /// <returns>A path to the tool or null if the path does not exist.</returns>
        internal static string FindSDKToolUsingToolsLocationHelper(string toolName)
        {
            // If it isn't there, we should find it in the SDK based on the version compiled into the utilities
            string pathToTool = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(toolName, TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.VersionLatest);
            return pathToTool;
        }

        /// <summary>
        /// Provide a method which can be used with a delegate to provide a specific FileExists behavior.
        ///
        /// Use FileInfo instead of File.Exists(...) because the latter fails silently (by design) if CAS
        /// doesn't grant access. We want the security exception if there is going to be one.
        /// </summary>
        /// <returns>True if the file exists. False if it does not</returns>
        private static bool FileExists(string filePath)
        {
            return new FileInfo(filePath).Exists;
        }
    }
}
