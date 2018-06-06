// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A packet which contains information needed for a node to configure itself for build.</summary>
//-----------------------------------------------------------------------

namespace Microsoft.Build.BackEnd
{
    internal sealed class LoggingNodeConfiguration : INodePacketTranslatable
    {
        private bool _includeEvaluationProfiles;
        private bool _includeTaskInputs;

        public bool IncludeEvaluationProfiles => _includeEvaluationProfiles;

        public bool IncludeTaskInputs => _includeTaskInputs;

        public LoggingNodeConfiguration()
        {
        }

        public LoggingNodeConfiguration(bool includeEvaluationProfiles, bool includeTaskInputs)
        {
            _includeEvaluationProfiles = includeEvaluationProfiles;
            _includeTaskInputs = includeTaskInputs;
        }

        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _includeEvaluationProfiles);
            translator.Translate(ref _includeTaskInputs);
        }
    }
}
