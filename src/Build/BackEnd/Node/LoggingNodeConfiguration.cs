// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal sealed class LoggingNodeConfiguration : ITranslatable
    {
        private bool _includeEvaluationMetaprojects;
        private bool _includeEvaluationProfiles;
        private bool _includeEvaluationPropertiesAndItemsInProjectStartedEvent;
        private bool _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent;
        private bool _includeTaskInputs;
        private bool _includeTargetOutputs;

        public bool IncludeEvaluationMetaprojects => _includeEvaluationMetaprojects;
        public bool IncludeEvaluationProfiles => _includeEvaluationProfiles;
        public bool IncludeEvaluationPropertiesAndItemsInProjectStartedEvent => _includeEvaluationPropertiesAndItemsInProjectStartedEvent;
        public bool IncludeEvaluationPropertiesAndItemsInEvaluationFinishedEvent => _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent;
        public bool IncludeTaskInputs => _includeTaskInputs;
        public bool IncludeTargetOutputs => _includeTargetOutputs;

        public LoggingNodeConfiguration()
        {
        }

        public LoggingNodeConfiguration(
            bool includeEvaluationMetaprojects,
            bool includeEvaluationProfiles,
            bool includeEvaluationPropertiesAndItemsInProjectStartedEvent,
            bool includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent,
            bool includeTaskInputs,
            bool includeTargetOutputs)
        {
            _includeEvaluationMetaprojects = includeEvaluationMetaprojects;
            _includeEvaluationProfiles = includeEvaluationProfiles;
            _includeEvaluationPropertiesAndItemsInProjectStartedEvent = includeEvaluationPropertiesAndItemsInProjectStartedEvent;
            _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent = includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent;
            _includeTaskInputs = includeTaskInputs;
            _includeTargetOutputs = includeTargetOutputs;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _includeEvaluationMetaprojects);
            translator.Translate(ref _includeEvaluationProfiles);
            translator.Translate(ref _includeEvaluationPropertiesAndItemsInProjectStartedEvent);
            translator.Translate(ref _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent);
            translator.Translate(ref _includeTaskInputs);
            translator.Translate(ref _includeTargetOutputs);
        }
    }
}
