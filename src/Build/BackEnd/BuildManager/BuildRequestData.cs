// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

[assembly: TypeForwardedTo(typeof(Microsoft.Build.Execution.BuildRequestDataFlags))]
namespace Microsoft.Build.Execution
{
    /// <summary>
    /// BuildRequestData encapsulates all the data needed to submit a build request.
    /// </summary>
    public class BuildRequestData : BuildRequestData<BuildRequestData, BuildResult>
    {
        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project instances.
        /// </summary>
        /// <param name="projectInstance">The instance to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        public BuildRequestData(ProjectInstance projectInstance, string[] targetsToBuild)
            : this(projectInstance, targetsToBuild, null, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project instances.
        /// </summary>
        /// <param name="projectInstance">The instance to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        public BuildRequestData(ProjectInstance projectInstance, string[] targetsToBuild, HostServices hostServices)
            : this(projectInstance, targetsToBuild, hostServices, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project instances.
        /// </summary>
        /// <param name="projectInstance">The instance to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        public BuildRequestData(ProjectInstance projectInstance, string[] targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags)
            : this(projectInstance, targetsToBuild, hostServices, flags, null)
        {
        }

        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project instances.
        /// </summary>
        /// <param name="projectInstance">The instance to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        /// <param name="propertiesToTransfer">The list of properties whose values should be transferred from the project to any out-of-proc node.</param>
        public BuildRequestData(ProjectInstance projectInstance, string[] targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags, IEnumerable<string>? propertiesToTransfer)
            : this(targetsToBuild, hostServices, flags, projectInstance.FullPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectInstance, nameof(projectInstance));

            foreach (string targetName in targetsToBuild)
            {
                ErrorUtilities.VerifyThrowArgumentNull(targetName, "target");
            }

            ProjectInstance = projectInstance;

            GlobalPropertiesDictionary = projectInstance.GlobalPropertiesDictionary;
            ExplicitlySpecifiedToolsVersion = projectInstance.ExplicitToolsVersion;
            if (propertiesToTransfer != null)
            {
                PropertiesToTransfer = new List<string>(propertiesToTransfer);
            }
        }

        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project instances.
        /// </summary>
        /// <param name="projectInstance">The instance to build.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use, if any.  May be null.</param>
        /// <param name="flags">Flags controlling this build request.</param>
        /// <param name="propertiesToTransfer">The list of properties whose values should be transferred from the project to any out-of-proc node.</param>
        /// <param name="requestedProjectState">A <see cref="Execution.RequestedProjectState"/> describing properties, items, and metadata that should be returned. Requires setting <see cref="BuildRequestDataFlags.ProvideSubsetOfStateAfterBuild"/>.</param>
        public BuildRequestData(ProjectInstance projectInstance, string[] targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags, IEnumerable<string>? propertiesToTransfer, RequestedProjectState requestedProjectState)
            : this(projectInstance, targetsToBuild, hostServices, flags, propertiesToTransfer)
        {
            ErrorUtilities.VerifyThrowArgumentNull(requestedProjectState, nameof(requestedProjectState));

            RequestedProjectState = requestedProjectState;
        }


        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project files.
        /// </summary>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="globalProperties">The global properties which should be used during evaluation of the project.  Cannot be null.</param>
        /// <param name="toolsVersion">The tools version to use for the build.  May be null.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use.  May be null.</param>
        public BuildRequestData(string projectFullPath, IDictionary<string, string?> globalProperties, string? toolsVersion, string[] targetsToBuild, HostServices? hostServices)
            : this(projectFullPath, globalProperties, toolsVersion, targetsToBuild, hostServices, BuildRequestDataFlags.None)
        {
        }

        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project files.
        /// </summary>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="globalProperties">The global properties which should be used during evaluation of the project.  Cannot be null.</param>
        /// <param name="toolsVersion">The tools version to use for the build.  May be null.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use.  May be null.</param>
        /// <param name="flags">The <see cref="BuildRequestDataFlags"/> to use.</param>
        /// <param name="requestedProjectState">A <see cref="Execution.RequestedProjectState"/> describing properties, items, and metadata that should be returned. Requires setting <see cref="BuildRequestDataFlags.ProvideSubsetOfStateAfterBuild"/>.</param>
        public BuildRequestData(string projectFullPath, IDictionary<string, string?> globalProperties,
            string? toolsVersion, string[] targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags,
            RequestedProjectState requestedProjectState)
            : this(projectFullPath, globalProperties, toolsVersion, targetsToBuild, hostServices, flags)
        {
            ErrorUtilities.VerifyThrowArgumentNull(requestedProjectState, nameof(requestedProjectState));

            RequestedProjectState = requestedProjectState;
        }

        /// <summary>
        /// Constructs a BuildRequestData for build requests based on project files.
        /// </summary>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="globalProperties">The global properties which should be used during evaluation of the project.  Cannot be null.</param>
        /// <param name="toolsVersion">The tools version to use for the build.  May be null.</param>
        /// <param name="targetsToBuild">The targets to build.</param>
        /// <param name="hostServices">The host services to use.  May be null.</param>
        /// <param name="flags">The <see cref="BuildRequestDataFlags"/> to use.</param>
        public BuildRequestData(string projectFullPath, IDictionary<string, string?> globalProperties, string? toolsVersion, string[] targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags)
            : this(targetsToBuild, hostServices, flags, FileUtilities.NormalizePath(projectFullPath)!)
        {
            ErrorUtilities.VerifyThrowArgumentLength(projectFullPath, nameof(projectFullPath));
            ErrorUtilities.VerifyThrowArgumentNull(globalProperties, nameof(globalProperties));

            GlobalPropertiesDictionary = new PropertyDictionary<ProjectPropertyInstance>(globalProperties.Count);
            foreach (KeyValuePair<string, string?> propertyPair in globalProperties)
            {
                GlobalPropertiesDictionary.Set(ProjectPropertyInstance.Create(propertyPair.Key, propertyPair.Value));
            }

            ExplicitlySpecifiedToolsVersion = toolsVersion;
        }

        /// <summary>
        /// Common constructor.
        /// </summary>
        private BuildRequestData(string[] targetsToBuild, HostServices? hostServices, BuildRequestDataFlags flags, string projectFullPath)
            : base(targetsToBuild, flags, hostServices)
        {
            ProjectFullPath = projectFullPath;
        }

        /// <summary>
        /// The actual project, in the case where the project doesn't come from disk.
        /// May be null.
        /// </summary>
        /// <value>The project instance.</value>
        public ProjectInstance? ProjectInstance
        {
            get;
        }

        /// <summary>The project file.</summary>
        /// <value>The project file to be built.</value>
        public string ProjectFullPath { get; internal set; }

        internal override BuildSubmissionBase<BuildRequestData, BuildResult> CreateSubmission(BuildManager buildManager,
            int submissionId, BuildRequestData requestData,
            bool legacyThreadingSemantics) =>
            new BuildSubmission(buildManager, submissionId, requestData, legacyThreadingSemantics);

        public override IEnumerable<string> EntryProjectsFullPath => ProjectFullPath.AsSingleItemEnumerable();

        /// <summary>
        /// The global properties to use.
        /// </summary>
        /// <value>The set of global properties to be used to build this request.</value>
        public ICollection<ProjectPropertyInstance> GlobalProperties => (GlobalPropertiesDictionary == null) ?
            (ICollection<ProjectPropertyInstance>)ReadOnlyEmptyCollection<ProjectPropertyInstance>.Instance :
            new ReadOnlyCollection<ProjectPropertyInstance>(GlobalPropertiesDictionary);

        public override bool IsGraphRequest => false;

        /// <summary>
        /// The explicitly requested tools version to use.
        /// </summary>
        public string? ExplicitlySpecifiedToolsVersion { get; }

        /// <summary>
        /// Returns a list of properties to transfer out of proc for the build.
        /// </summary>
        public IEnumerable<string>? PropertiesToTransfer { get; }

        /// <summary>
        /// Returns the properties, items, and metadata that will be returned
        /// by this build.
        /// </summary>
        public RequestedProjectState? RequestedProjectState { get; }

        /// <summary>
        /// Whether the tools version used originated from an explicit specification,
        /// for example from an MSBuild task or /tv switch.
        /// </summary>
        internal bool ExplicitToolsVersionSpecified => ExplicitlySpecifiedToolsVersion != null;

        /// <summary>
        /// Returns the global properties as a dictionary.
        /// </summary>
        internal PropertyDictionary<ProjectPropertyInstance>? GlobalPropertiesDictionary { get; }

        private IReadOnlyDictionary<string, string?>? _globalPropertiesLookup;

        /// <inheritdoc cref="BuildRequestDataBase"/>
        public override IReadOnlyDictionary<string, string?> GlobalPropertiesLookup => _globalPropertiesLookup ??=
            Execution.GlobalPropertiesLookup.ToGlobalPropertiesLookup(GlobalPropertiesDictionary);

        // WARNING!: Do not remove the below proxy properties.
        //  They are required to make the OM forward compatible
        //  (code built against this OM should run against binaries with previous version of OM).

        /// <inheritdoc cref="BuildRequestDataBase.TargetNames"/>
        public new ICollection<string> TargetNames => base.TargetNames;

        /// <inheritdoc cref="BuildRequestDataBase.Flags"/>
        public new BuildRequestDataFlags Flags => base.Flags;

        /// <inheritdoc cref="BuildRequestDataBase.HostServices"/>
        public new HostServices? HostServices => base.HostServices;
    }
}
