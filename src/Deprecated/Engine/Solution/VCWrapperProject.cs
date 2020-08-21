// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Win32;

namespace Microsoft.Build.BuildEngine
{
    internal class VCWrapperProject
    {
        /// <summary>
        /// Add a target for a VC project file into the XML doc that's being generated.
        /// This is used only when building standalone VC projects
        /// </summary>
        /// <param name="msbuildProject"></param>
        /// <param name="projectPath"></param>
        /// <param name="targetName"></param>
        /// <param name="subTargetName"></param>
        /// <owner>RGoel</owner>
        static private void AddVCBuildTarget
        (
            Project msbuildProject,
            string projectPath,
            string targetName,
            string subTargetName
        )
        {
            Target newTarget = msbuildProject.Targets.AddNewTarget(targetName);
            if (subTargetName == "Publish")
            {
                // Well, hmmm.  The VCBuild doesn't support any kind of 
                // a "Publish" operation.  The best we can really do is offer up a 
                // message saying so.
                SolutionWrapperProject.AddErrorWarningMessageElement(newTarget, XMakeElements.error, true, "SolutionVCProjectNoPublish");
            }
            else
            {
                SolutionWrapperProject.AddErrorWarningMessageElement(newTarget, XMakeElements.warning, true, "StandaloneVCProjectP2PRefsBroken");

                string projectFullPath = Path.GetFullPath(projectPath);
                AddVCBuildTaskElement(msbuildProject, newTarget, "$(VCBuildSolutionFile)", projectFullPath, subTargetName, "$(PlatformName)", "$(ConfigurationName)");
            }
        }

        /// <summary>
        /// Adds a new VCBuild task element to the specified target
        /// </summary>
        /// <param name="target">The target to add the VCBuild task to</param>
        /// <param name="solutionPath">Path to the solution if any</param>
        /// <param name="projectPath">Path to the solution if any</param>
        /// <param name="vcbuildTargetName">The VCBuild target name</param>
        /// <param name="platformName">The platform parameter to VCBuild</param>
        /// <param name="fullConfigurationName">Configuration property value</param>
        /// <returns></returns>
        static internal BuildTask AddVCBuildTaskElement
        (
            Project msbuildProject,
            Target target,
            string solutionPath,
            string projectPath,
            string vcbuildTargetName,
            string platformName,
            string fullConfigurationName
        )
        {
            // The VCBuild task (which we already shipped) has a bug - it cannot
            // find vcbuild.exe when running in MSBuild 64 bit unless it's on the path.
            // So, pass it here, unless some explicit path was passed.
            // Note, we have to do this even if we're in a 32 bit MSBuild, because we save the .sln.cache
            // file, and the next build of the solution could be a 64 bit MSBuild.

            if (VCBuildLocationHint != null) // Should only be null if vcbuild truly isn't installed; in that case, let the task log its error
            {
                BuildTask createProperty = target.AddNewTask("CreateProperty");

                createProperty.SetParameterValue("Value", VCBuildLocationHint);
                createProperty.Condition = "'$(VCBuildToolPath)' == ''";
                createProperty.AddOutputProperty("Value", "VCBuildToolPath");
            }

            BuildTask newTask = target.AddNewTask("VCBuild");

            newTask.SetParameterValue("Projects", projectPath, true /* treat as literal */);

            // Add the toolpath so that the user can override if necessary
            newTask.SetParameterValue("ToolPath", "$(VCBuildToolPath)");

            newTask.SetParameterValue("Configuration", fullConfigurationName);

            if (!string.IsNullOrEmpty(platformName))
            {
                newTask.SetParameterValue("Platform", platformName);
            }

            newTask.SetParameterValue("SolutionFile", solutionPath);

            if (!string.IsNullOrEmpty(vcbuildTargetName))
            {
                newTask.SetParameterValue(vcbuildTargetName, "true");
            }

            // Add the override switch so that the user can supply one if necessary
            newTask.SetParameterValue("Override", "$(VCBuildOverride)");

            // Add any additional lib paths
            newTask.SetParameterValue("AdditionalLibPaths", "$(VCBuildAdditionalLibPaths)");

            // Only use new properties if we're not emitting a 2.0 project
            if (!String.Equals(msbuildProject.ToolsVersion, "2.0", StringComparison.OrdinalIgnoreCase))
            {
                // Add any additional link library paths
                newTask.SetParameterValue("AdditionalLinkLibraryPaths", "$(VCBuildAdditionalLinkLibraryPaths)");

                // Add the useenv switch so that the user can supply one if necessary
                // Note: "VCBuildUserEnvironment" is included for backwards-compatibility; the correct
                // property name is "VCBuildUseEnvironment" to match the task parameter. When the old name is
                // used the task will emit a warning.
                newTask.SetParameterValue("UseEnvironment", "$(VCBuildUseEnvironment)");
            }

            newTask.SetParameterValue("UserEnvironment", "$(VCBuildUserEnvironment)");

            // Add the additional options switches
            newTask.SetParameterValue("AdditionalOptions", "$(VCBuildAdditionalOptions)");

            return newTask;
        }

        /// <summary>
        /// This method generates an XmlDocument representing an MSBuild project wrapper for a VC project
        /// </summary>
        /// <owner>LukaszG</owner>
        static internal XmlDocument GenerateVCWrapperProject(Engine parentEngine, string vcProjectFilename, string toolsVersion)
        {
            string projectPath = Path.GetFullPath(vcProjectFilename);
            Project msbuildProject;
            try
            {
                msbuildProject = new Project(parentEngine, toolsVersion);
            }
            catch (InvalidOperationException)
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo(projectPath);
                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, "UnrecognizedToolsVersion", toolsVersion);
                throw new InvalidProjectFileException(projectPath, fileInfo.Line, fileInfo.Column, fileInfo.EndLine, fileInfo.EndColumn, message, null, errorCode, helpKeyword);
            }

            msbuildProject.IsLoadedByHost = false;
            msbuildProject.DefaultTargets = "Build";

            string wrapperProjectToolsVersion = SolutionWrapperProject.DetermineWrapperProjectToolsVersion(toolsVersion);
            msbuildProject.DefaultToolsVersion = wrapperProjectToolsVersion;

            BuildPropertyGroup propertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            propertyGroup.Condition = " ('$(Configuration)' != '') and ('$(Platform)' == '') ";
            propertyGroup.AddNewProperty("ConfigurationName", "$(Configuration)");

            propertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            propertyGroup.Condition = " ('$(Configuration)' != '') and ('$(Platform)' != '') ";
            propertyGroup.AddNewProperty("ConfigurationName", "$(Configuration)|$(Platform)");

            // only use PlatformName if we only have the platform part
            propertyGroup = msbuildProject.AddNewPropertyGroup(true /* insertAtEndOfProject = true */);
            propertyGroup.Condition = " ('$(Configuration)' == '') and ('$(Platform)' != '') ";
            propertyGroup.AddNewProperty("PlatformName", "$(Platform)");

            AddVCBuildTarget(msbuildProject, projectPath, "Build", null);
            AddVCBuildTarget(msbuildProject, projectPath, "Clean", "Clean");
            AddVCBuildTarget(msbuildProject, projectPath, "Rebuild", "Rebuild");
            AddVCBuildTarget(msbuildProject, projectPath, "Publish", "Publish");

            // Special environment variable to allow people to see the in-memory MSBuild project generated
            // to represent the VC project.
            if (Environment.GetEnvironmentVariable("MSBuildEmitSolution") != null)
            {
                msbuildProject.Save(vcProjectFilename + ".proj");
            }

            return msbuildProject.XmlDocument;
        }

        /// <summary>
        /// Hint to give the VCBuild task to help it find vcbuild.exe.
        /// </summary>
        static private string path;

        /// <summary>
        /// Hint to give the VCBuild task to help it find vcbuild.exe.
        /// Directory in which vcbuild.exe is found.
        /// </summary>
        static internal string VCBuildLocationHint
        {
            get
            {
                if (path == null)
                {
                    path = GenerateFullPathToTool(RegistryView.Default);

                    if (path == null && Environment.Is64BitProcess)
                    {
                        path = GenerateFullPathToTool(RegistryView.Registry32);
                    }

                    if (path != null)
                    {
                        path = Path.GetDirectoryName(path);
                    }
                }

                return path;
            }
        }

        // The code below is mostly copied from the VCBuild task that we shipped in 3.5.
        // It is the logic it uses to find vcbuild.exe. That logic had a flaw - 
        // in 64 bit MSBuild, in a vanilla command window (like in Team Build) it would not
        // find vcbuild.exe. We use the logic below to predict whether VCBuild will find it,
        // and if it won't, we will pass the "hint" to use the 64 bit program files location.

        /// <summary>
        /// constants for VS9 Pro and above SKUs
        /// </summary>
        // root registry key for VS9
        private const string vs9RegKey = @"SOFTWARE\Microsoft\VisualStudio\9.0";
        // the name of the value containing disk install directory for the IDE components 
        // ("...\common7\ide" for layouts)
        private const string vs9InstallDirValueName = "InstallDir";
        // relative path from the above directory to vcbuild.exe on layouts
        private const string vs9RelativePathToVCBuildLayouts = @"..\..\vc\vcpackages\vcbuild.exe";
        // relative path from the above directory to vcbuild.exe on batch
        private const string vs9RelativePathToVCBuildBatch = @"vcbuild.exe";

        /// <summary>
        /// constants for the VC9 Express SKU
        /// </summary>
        // root registry key for VC9
        private const string vc9RegKey = @"SOFTWARE\Microsoft\VCExpress\9.0";
        // the name of the value containing disk install directory for the IDE components 
        // ("...\common7\ide" for layouts)
        private const string vc9InstallDirValueName = "InstallDir";
        // relative path from the above directory to vcbuild.exe on layouts
        private const string vc9RelativePathToVCBuildLayouts = @"..\..\vc\vcpackages\vcbuild.exe";
        // relative path from the above directory to vcbuild.exe on batch
        private const string vc9RelativePathToVCBuildBatch = @"vcbuild.exe";

        // name of the tool
        private const string vcbuildName = "vcbuild.exe";

        /// <summary>
        /// Determing the path to vcbuild.exe
        /// </summary>
        /// <returns>path to vcbuild.exe, or null if it's not found</returns>
        private static string GenerateFullPathToTool(RegistryView registryView)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
            {
                // try VS9 professional and above SKUs first
                string location = TryLocationFromRegistry(baseKey, vs9RegKey, vs9InstallDirValueName,
                    vs9RelativePathToVCBuildLayouts, vs9RelativePathToVCBuildBatch);

                if (location != null)
                {
                    return location;
                }

                // fall back to the VC Express SKU
                location = TryLocationFromRegistry(baseKey, vc9RegKey, vc9InstallDirValueName,
                    vc9RelativePathToVCBuildLayouts, vc9RelativePathToVCBuildBatch);

                if (location != null)
                {
                    return location;
                }

                // finally, try looking in the system path
                if (Microsoft.Build.BuildEngine.Shared.NativeMethods.FindOnPath(vcbuildName) == null)
                {
                    // If SearchPath didn't find the file, it's not on the system path and we have no chance of finding it.
                    return null;
                }

                return null;
            }
        }

        /// <summary>
        /// Looks up a path from the registry if present, and checks whether VCBuild.exe is there.
        /// </summary>
        /// <param name="subKey">Registry key to open</param>
        /// <param name="valueName">Value under that key to read</param>
        /// <param name="messageToLogIfNotFound">Low-pri message to log if registry key isn't found</param>
        /// <param name="relativePathFromValueOnLayout">Relative path from the key value to vcbuild.exe for layout installs</param>
        /// <param name="relativePathFromValueOnBatch">Relative path from the key value to vcbuild.exe for batch installs</param>
        /// <returns>Path to vcbuild.exe, or null if it's not found</returns>
        /// <owner>danmose</owner>
        private static string TryLocationFromRegistry(RegistryKey root, string subKeyName, string valueName,
            string relativePathFromValueOnLayout, string relativePathFromValueOnBatch)
        {
            using (RegistryKey subKey = root.OpenSubKey(subKeyName))
            {
                if (subKey == null)
                {
                    // We couldn't find an installation of the product we were looking for.
                    return null;
                }
                else
                {
                    string rootDir = (string)subKey.GetValue(valueName);

                    if (rootDir != null)
                    {
                        // first try the location for layouts
                        string vcBuildPath = Path.Combine(rootDir, relativePathFromValueOnLayout);
                        if (File.Exists(vcBuildPath))
                        {
                            return vcBuildPath;
                        }

                        // if not found in layouts location, try the alternate dir if any,
                        // which contains vcbuild for batch installs
                        if (relativePathFromValueOnBatch != null)
                        {
                            vcBuildPath = Path.Combine(rootDir, relativePathFromValueOnBatch);
                            if (File.Exists(vcBuildPath))
                            {
                                return vcBuildPath;
                            }
                        }
                    }
                }

                // Didn't find it
                return null;
            }
        }
    }
}
