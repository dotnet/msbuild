// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// Represents an SDK resolver request which is serialized and sent between nodes.  This is mostly a wrapper around <see cref="SdkReference"/>
    /// with an additional <see cref="INodePacket"/> implementation.
    /// </summary>
    internal sealed class SdkResolverRequest : INodePacket
    {
        private BuildEventContext _buildEventContext;
        private ElementLocation _elementLocation;
        private string _minimumVersion;
        private string _name;
        private string _projectPath;
        private string _solutionPath;
        private int _submissionId;
        private string _version;
        private bool _interactive;

        public SdkResolverRequest(INodePacketTranslator translator)
        {
            Translate(translator);
        }

        private SdkResolverRequest(int submissionId, string name, string version, string minimumVersion, BuildEventContext buildEventContext, ElementLocation elementLocation, string solutionPath, string projectPath, bool interactive)
        {
            _buildEventContext = buildEventContext;
            _submissionId = submissionId;
            _elementLocation = elementLocation;
            _minimumVersion = minimumVersion;
            _name = name;
            _projectPath = projectPath;
            _solutionPath = solutionPath;
            _version = version;
            _interactive = interactive;
        }

        public BuildEventContext BuildEventContext => _buildEventContext;

        public ElementLocation ElementLocation => _elementLocation;

        public bool Interactive => _interactive;

        public string MinimumVersion => _minimumVersion;

        public string Name => _name;

        public int NodeId { get; set; }

        public string ProjectPath => _projectPath;

        public string SolutionPath => _solutionPath;

        public int SubmissionId => _submissionId;

        public NodePacketType Type => NodePacketType.ResolveSdkRequest;

        public string Version => _version;

        public static SdkResolverRequest Create(int submissionId, SdkReference sdkReference, BuildEventContext buildEventContext, ElementLocation elementLocation, string solutionPath, string projectPath, bool interactive)
        {
            return new SdkResolverRequest(submissionId, sdkReference.Name, sdkReference.Version, sdkReference.MinimumVersion, buildEventContext, elementLocation, solutionPath, projectPath, interactive);
        }

        public static INodePacket FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new SdkResolverRequest(translator);
        }

        public void Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _buildEventContext);
            translator.Translate(ref _elementLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _minimumVersion);
            translator.Translate(ref _name);
            translator.Translate(ref _projectPath);
            translator.Translate(ref _solutionPath);
            translator.Translate(ref _submissionId);
            translator.Translate(ref _version);
            translator.Translate(ref _interactive);
        }
    }
}
