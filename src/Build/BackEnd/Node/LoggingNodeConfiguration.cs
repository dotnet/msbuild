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

        public bool IncludeEvaluationMetaprojects => _includeEvaluationMetaprojects;
        public bool IncludeEvaluationProfiles => _includeEvaluationProfiles;
        public bool IncludeEvaluationPropertiesAndItemsInProjectStartedEvent => _includeEvaluationPropertiesAndItemsInProjectStartedEvent;
        public bool IncludeEvaluationPropertiesAndItemsInEvaluationFinishedEvent => _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent;
        public bool IncludeTaskInputs => _includeTaskInputs;

        public LoggingNodeConfiguration()
        {
        }

        public LoggingNodeConfiguration(
            bool includeEvaluationMetaprojects,
            bool includeEvaluationProfiles,
            bool includeEvaluationPropertiesAndItemsInProjectStartedEvent,
            bool includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent,
            bool includeTaskInputs)
        {
            _includeEvaluationMetaprojects = includeEvaluationMetaprojects;
            _includeEvaluationProfiles = includeEvaluationProfiles;
            _includeEvaluationPropertiesAndItemsInProjectStartedEvent = includeEvaluationPropertiesAndItemsInProjectStartedEvent;
            _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent = includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent;
            _includeTaskInputs = includeTaskInputs;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _includeEvaluationMetaprojects);
            translator.Translate(ref _includeEvaluationProfiles);
            translator.Translate(ref _includeEvaluationPropertiesAndItemsInProjectStartedEvent);
            translator.Translate(ref _includeEvaluationPropertiesAndItemsInEvaluationFinishedEvent);
            translator.Translate(ref _includeTaskInputs);
        }
    }
}
