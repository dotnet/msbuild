// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A build request contains information about the configuration used to build as well
    /// as which targets need to be built.
    /// </summary>
    internal class BuildRequest : INodePacket
    {
        /// <summary>
        /// The invalid global request id
        /// </summary>
        public const int InvalidGlobalRequestId = -1;

        /// <summary>
        /// The invalid node request id
        /// </summary>
        public const int InvalidNodeRequestId = 0;

        /// <summary>
        /// The results transfer request id
        /// </summary>
        public const int ResultsTransferNodeRequestId = -1;

        /// <summary>
        /// The submission with which this request is associated.
        /// </summary>
        private int _submissionId;

        /// <summary>
        /// The configuration id.
        /// </summary>
        private int _configurationId;

        /// <summary>
        /// The project context id, if already determined.
        /// </summary>
        private int _projectContextId;

        /// <summary>
        /// The global build request id, assigned by the Build Manager
        /// </summary>
        private int _globalRequestId;

        /// <summary>
        /// The global request id of the request which spawned this one.
        /// </summary>
        private int _parentGlobalRequestId;

        /// <summary>
        /// The build request id assigned by the node originating this request.
        /// </summary>
        private int _nodeRequestId;

        /// <inheritdoc cref="BuildRequest.Targets"/>
        private List<string> _targets;

        /// <inheritdoc cref="BuildRequest.ProxyTargets"/>
        private ProxyTargets _proxyTargets;

        /// <summary>
        /// The build event context of the parent
        /// </summary>
        private BuildEventContext _parentBuildEventContext;

        /// <summary>
        /// The build event context of this request
        /// </summary>
        private BuildEventContext _buildEventContext;

        /// <summary>
        /// Whether or not the <see cref="BuildResult"/> issued in response to this request should include <see cref="BuildResult.ProjectStateAfterBuild"/>.
        /// </summary>
        private BuildRequestDataFlags _buildRequestDataFlags;

        /// <summary>
        /// Filter describing properties, items, and metadata of interest for this request.
        /// </summary>
        private RequestedProjectState _requestedProjectState;

        private bool _skipStaticGraphIsolationConstraints;

        /// <summary>
        /// Constructor for serialization.
        /// </summary>
        public BuildRequest()
        {
        }

        private BuildRequest(
            int submissionId,
            int nodeRequestId,
            int configurationId,
            HostServices hostServices,
            BuildRequestDataFlags buildRequestDataFlags,
            RequestedProjectState requestedProjectState,
            int projectContextId)
        {
            _submissionId = submissionId;
            _configurationId = configurationId;
            _projectContextId = projectContextId;

            HostServices = hostServices;
            _buildEventContext = BuildEventContext.Invalid;
            _globalRequestId = InvalidGlobalRequestId;

            _nodeRequestId = nodeRequestId;
            _buildRequestDataFlags = buildRequestDataFlags;
            _requestedProjectState = requestedProjectState;
        }

        /// <summary>
        /// Initializes a build request with proxy targets.
        /// </summary>
        /// <param name="submissionId">The id of the build submission.</param>
        /// <param name="nodeRequestId">The id of the node issuing the request</param>
        /// <param name="configurationId">The configuration id to use.</param>
        /// <param name="proxyTargets"><see cref="ProxyTargets"/></param>
        /// <param name="hostServices">Host services if any. May be null.</param>
        /// <param name="buildRequestDataFlags">Additional flags for the request.</param>
        /// <param name="requestedProjectState">Filter for desired build results.</param>
        /// <param name="projectContextId">The project context id</param>
        public BuildRequest(
            int submissionId,
            int nodeRequestId,
            int configurationId,
            ProxyTargets proxyTargets,
            HostServices hostServices,
            BuildRequestDataFlags buildRequestDataFlags = BuildRequestDataFlags.None,
            RequestedProjectState requestedProjectState = null,
            int projectContextId = BuildEventContext.InvalidProjectContextId)
            : this(submissionId, nodeRequestId, configurationId, hostServices, buildRequestDataFlags, requestedProjectState, projectContextId)
        {
            _proxyTargets = proxyTargets;
            _targets = proxyTargets.ProxyTargetToRealTargetMap.Keys.ToList();

            // Only root requests can have proxy targets.
            _parentGlobalRequestId = InvalidGlobalRequestId;
            _parentBuildEventContext = BuildEventContext.Invalid;
        }

        /// <summary>
        /// Initializes a build request with a parent context.
        /// </summary>
        /// <param name="submissionId">The id of the build submission.</param>
        /// <param name="nodeRequestId">The id of the node issuing the request</param>
        /// <param name="configurationId">The configuration id to use.</param>
        /// <param name="escapedTargets">The targets to be built</param>
        /// <param name="hostServices">Host services if any. May be null.</param>
        /// <param name="parentBuildEventContext">The build event context of the parent project.</param>
        /// <param name="parentRequest">The parent build request, if any.</param>
        /// <param name="skipStaticGraphIsolationConstraints"></param>
        /// <param name="buildRequestDataFlags">Additional flags for the request.</param>
        /// <param name="requestedProjectState">Filter for desired build results.</param>
        /// <param name="projectContextId">The project context id</param>
        public BuildRequest(
            int submissionId,
            int nodeRequestId,
            int configurationId,
            ICollection<string> escapedTargets,
            HostServices hostServices,
            BuildEventContext parentBuildEventContext,
            BuildRequest parentRequest,
            BuildRequestDataFlags buildRequestDataFlags = BuildRequestDataFlags.None,
            RequestedProjectState requestedProjectState = null,
            bool skipStaticGraphIsolationConstraints = false,
            int projectContextId = BuildEventContext.InvalidProjectContextId)
        : this(submissionId, nodeRequestId, configurationId, hostServices, buildRequestDataFlags, requestedProjectState, projectContextId)
        {
            ErrorUtilities.VerifyThrowArgumentNull(escapedTargets, "targets");
            ErrorUtilities.VerifyThrowArgumentNull(parentBuildEventContext, nameof(parentBuildEventContext));

            // When targets come into a build request, we unescape them.
            _targets = new List<string>(escapedTargets.Count);
            foreach (string target in escapedTargets)
            {
                _targets.Add(EscapingUtilities.UnescapeAll(target));
            }

            _parentBuildEventContext = parentBuildEventContext;
            _parentGlobalRequestId = parentRequest?.GlobalRequestId ?? InvalidGlobalRequestId;

            _skipStaticGraphIsolationConstraints = skipStaticGraphIsolationConstraints;
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private BuildRequest(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Returns true if the configuration has been resolved, false otherwise.
        /// </summary>
        public bool IsConfigurationResolved
        {
            [DebuggerStepThrough]
            get
            { return _configurationId > 0; }
        }

        /// <summary>
        /// Returns the submission id
        /// </summary>
        public int SubmissionId
        {
            [DebuggerStepThrough]
            get
            { return _submissionId; }
        }

        /// <summary>
        /// Returns the configuration id
        /// </summary>
        public int ConfigurationId
        {
            [DebuggerStepThrough]
            get
            { return _configurationId; }
        }

        /// <summary>
        /// Returns the project context id
        /// </summary>
        public int ProjectContextId
        {
            [DebuggerStepThrough]
            get
            { return _projectContextId; }
        }

        /// <summary>
        /// Gets the global request id
        /// </summary>
        public int GlobalRequestId
        {
            [DebuggerStepThrough]
            get
            {
                return _globalRequestId;
            }

            set
            {
                ErrorUtilities.VerifyThrow(_globalRequestId == InvalidGlobalRequestId, "Global Request ID cannot be set twice.");
                _globalRequestId = value;
            }
        }

        /// <summary>
        /// Gets the global request id of the parent request.
        /// </summary>
        public int ParentGlobalRequestId
        {
            [DebuggerStepThrough]
            get
            { return _parentGlobalRequestId; }
        }

        /// <summary>
        /// Gets the node request id
        /// </summary>
        public int NodeRequestId
        {
            [DebuggerStepThrough]
            get
            { return _nodeRequestId; }

            [DebuggerStepThrough]
            set
            { _nodeRequestId = value; }
        }

        /// <summary>
        /// The targets specified when the request was made.  Doesn't include default or initial targets.
        /// Either this is null or <see cref="ProxyTargets"/> is null;
        /// </summary>
        public List<string> Targets
        {
            [DebuggerStepThrough]
            get
            { return _targets; }
        }

        /// <summary>
        /// See <see cref="ProxyTargets"/>. Either this is null, or <see cref="_targets"/> is null;
        /// </summary>
        public ProxyTargets ProxyTargets
        {
            [DebuggerStepThrough]
            get
            { return _proxyTargets; }
        }

        /// <summary>
        /// Returns the type of packet.
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.BuildRequest; }
        }

        /// <summary>
        /// Returns the build event context of the parent, if any.
        /// </summary>
        public BuildEventContext ParentBuildEventContext
        {
            [DebuggerStepThrough]
            get
            { return _parentBuildEventContext; }
        }

        /// <summary>
        /// Returns the build event context for this request, if any.
        /// </summary>
        public BuildEventContext BuildEventContext
        {
            [DebuggerStepThrough]
            get
            {
                return _buildEventContext;
            }

            set
            {
                ErrorUtilities.VerifyThrow(_buildEventContext == BuildEventContext.Invalid, "The build event context is already set.");
                _buildEventContext = value;
            }
        }

        /// <summary>
        /// The <see cref="BuildEventContext" /> of the currently executing task, if any.
        /// Used to correlate a project's build with the parent task that spawned it
        /// (usually the MSBuild task).
        /// </summary>
        public BuildEventContext CurrentTaskContext { get; set; }

        /// <summary>
        /// The set of flags specified in the BuildRequestData for this request.
        /// </summary>
        public BuildRequestDataFlags BuildRequestDataFlags
        {
            get => _buildRequestDataFlags;
            set => _buildRequestDataFlags = value;
        }

        /// <summary>
        /// Filter describing properties, items, and metadata of interest for this request.
        /// </summary>
        public RequestedProjectState RequestedProjectState
        {
            get => _requestedProjectState;
            set => _requestedProjectState = value;
        }

        /// <summary>
        /// The route for host-aware tasks back to the host
        /// </summary>
        internal HostServices HostServices
        {
            [DebuggerStepThrough]
            get => _hostServices;
            set => _hostServices = value;
        }

        private HostServices _hostServices;

        /// <summary>
        /// Returns true if this is a root request (one which has no parent.)
        /// </summary>
        internal bool IsRootRequest
        {
            [DebuggerStepThrough]
            get
            { return _parentGlobalRequestId == InvalidGlobalRequestId; }
        }

        /// <summary>
        /// Whether static graph isolation constraints should be skipped for this request
        /// </summary>
        internal bool SkipStaticGraphIsolationConstraints => _skipStaticGraphIsolationConstraints;

        /// <summary>
        /// Sets the configuration id to a resolved id.
        /// </summary>
        /// <param name="newConfigId">The new configuration id for this request.</param>
        public void ResolveConfiguration(int newConfigId)
        {
            ErrorUtilities.VerifyThrow(!IsConfigurationResolved, "Configuration already resolved");
            _configurationId = newConfigId;
            ErrorUtilities.VerifyThrow(IsConfigurationResolved, "Configuration not resolved");
        }

        #region INodePacket Members

        /// <summary>
        /// Reads/writes this packet
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _submissionId);
            translator.Translate(ref _configurationId);
            translator.Translate(ref _globalRequestId);
            translator.Translate(ref _parentGlobalRequestId);
            translator.Translate(ref _nodeRequestId);
            translator.Translate(ref _targets);
            translator.Translate(ref _parentBuildEventContext);
            translator.Translate(ref _buildEventContext);
            translator.TranslateEnum(ref _buildRequestDataFlags, (int)_buildRequestDataFlags);
            translator.Translate(ref _skipStaticGraphIsolationConstraints);
            translator.Translate(ref _requestedProjectState);
            translator.Translate(ref _hostServices);
            translator.Translate(ref _proxyTargets, ProxyTargets.FactoryForDeserialization);
            translator.Translate(ref _projectContextId);

            // UNDONE: (Compat) Serialize the host object.
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new BuildRequest(translator);
        }

        #endregion

        public bool IsProxyBuildRequest()
        {
            return ProxyTargets != null;
        }
    }
}
