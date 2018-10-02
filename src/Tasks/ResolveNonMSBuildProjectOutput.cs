// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// This task determines the output files for non-MSBuild project references. We look inside 
    /// a special property set by the VS IDE for the list of project guids and their associated outputs.
    /// While there's nothing that would prevent resolution of MSBuild projects in this task, the IDE
    /// only pre-resolves non-MSBuild projects so that we can separate MSBuild project references from
    /// non-MSBuild ones and return the list of MSBuild projects as UnresolvedProjectReferences.
    /// Then we can use more powerful MSBuild mechanisms to manipulate just the MSBuild project 
    /// references (i.e. calling into specific targets of references to get the manifest file name) 
    /// which would not be possible with a mixed list of MSBuild & non-MSBuild references.
    /// </remarks>
    public class ResolveNonMSBuildProjectOutput : ResolveProjectBase
    {
        #region Constructors

        /// <summary>
        /// default public constructor
        /// </summary>
        public ResolveNonMSBuildProjectOutput()
        {
            // do nothing
        }

        #endregion

        #region Properties

        /// <summary>
        /// A special XML string containing resolved project outputs - we need to simply match the projects and
        /// return the appropriate paths
        /// </summary>
        public string PreresolvedProjectOutputs { get; set; }

        /// <summary>
        /// The list of resolved reference paths (preserving the original project reference attributes)
        /// </summary>
        [Output]
        public ITaskItem[] ResolvedOutputPaths { get; set; }

        /// <summary>
        /// The list of project reference items that could not be resolved using the pre-resolved list of outputs.
        /// Since VS only pre-resolves non-MSBuild projects, this means that project references in this list
        /// are in the MSBuild format.
        /// </summary>
        [Output]
        public ITaskItem[] UnresolvedProjectReferences { get; set; }

        /// <summary>
        /// A delegate with a signature that matches AssemblyName.GetAssemblyName.
        /// </summary>
        internal delegate AssemblyName GetAssemblyNameDelegate(string path);

        /// <summary>
        /// A dependency-injection way of getting an assembly name.
        /// </summary>
        internal GetAssemblyNameDelegate GetAssemblyName { get; set; }

        #endregion

        #region ITask Members

        /// <summary>
        /// Main task method
        /// </summary>
        public override bool Execute()
        {
            // Allow unit tests to inject a non-file system dependent version of this.
            if (GetAssemblyName == null)
            {
                GetAssemblyName = AssemblyName.GetAssemblyName;
            }

            try
            {
                if (!VerifyProjectReferenceItems(ProjectReferences, false /* treat problems as warnings */))
                {
                    return false;
                }

                var resolvedPaths = new List<ITaskItem>(ProjectReferences.GetLength(0));
                var unresolvedReferences = new List<ITaskItem>(ProjectReferences.GetLength(0));

                CacheProjectElementsFromXml(PreresolvedProjectOutputs);

                foreach (ITaskItem projectRef in ProjectReferences)
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "ResolveNonMSBuildProjectOutput.ProjectReferenceResolutionStarting", projectRef.ItemSpec);

                    bool resolveSuccess = ResolveProject(projectRef, out ITaskItem resolvedPath);
                    if (resolveSuccess)
                    {
                        if (resolvedPath.ItemSpec.Length > 0)
                        {
                            // VC project system does not look like an MSBuild project type yet because VC do not
                            // yet implement IVSProjectBuildSystem.  So project references to VC managed libraries
                            // need to be recognized as such.
                            // Even after VC implements this IVSProjectBuildSystem, other project types (NMake, NAnt, etc.)
                            // still can generate managed assemblies, in which case we still need to perform this managed
                            // assembly check.
                            try
                            {
                                GetAssemblyName(resolvedPath.ItemSpec);
                                resolvedPath.SetMetadata("ManagedAssembly", "true");
                            }
                            catch (BadImageFormatException)
                            {
                            }

                            resolvedPaths.Add(resolvedPath);

                            Log.LogMessageFromResources(MessageImportance.Low, "ResolveNonMSBuildProjectOutput.ProjectReferenceResolutionSuccess", projectRef.ItemSpec, resolvedPath.ItemSpec);
                        }
                        // If resolved path is empty, log a warning. This means that this reference is not an MSBuild reference,
                        // but could not be resolved in the IDE.
                        else
                        {
                            Log.LogWarningWithCodeFromResources("ResolveNonMSBuildProjectOutput.ProjectReferenceResolutionFailure", projectRef.ItemSpec);
                        }
                    }
                    else
                    {
                        unresolvedReferences.Add(projectRef);

                        // This is not an error - we pass unresolved references to UnresolvedProjectReferences for further
                        // processing in the .targets file.
                        Log.LogMessageFromResources(MessageImportance.Low, "ResolveNonMSBuildProjectOutput.ProjectReferenceUnresolved", projectRef.ItemSpec);
                    }
                }

                ResolvedOutputPaths = resolvedPaths.ToArray();
                UnresolvedProjectReferences = unresolvedReferences.ToArray();
            }
            catch (XmlException e)
            {
                Log.LogErrorWithCodeFromResources("General.ErrorExecutingTask", this.GetType().Name, e.Message);
                return false;
            }

            return true;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Given a project reference task item and an XML document containing pre-resolved output paths, 
        /// find the output path for that task item.
        /// </summary>
        /// <param name="resolvedPath">resulting ITaskItem containing the resolved path</param>
        /// <returns>true if resolved successfully</returns>
        internal bool ResolveProject(ITaskItem projectRef, out ITaskItem resolvedPath)
        {
            string projectOutputPath = GetProjectItem(projectRef);
            if (projectOutputPath != null)
            {
                resolvedPath = new TaskItem(projectOutputPath);
                projectRef.CopyMetadataTo(resolvedPath);
                return true;
            }

            resolvedPath = null;
            return false;
        }

        #endregion
    }
}

