// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal sealed class LoggingNodeConfiguration : ITranslatable
    {
        private bool _includeEvaluationMetaprojects;
        private bool _includeEvaluationProfiles;
        private bool _includeEvaluationPropertiesAndItems;
        private bool _includeTaskInputs;

        public bool IncludeEvaluationMetaprojects => _includeEvaluationMetaprojects;
        public bool IncludeEvaluationProfiles => _includeEvaluationProfiles;
        public bool IncludeEvaluationPropertiesAndItems => _includeEvaluationPropertiesAndItems;
        public bool IncludeTaskInputs => _includeTaskInputs;

        public LoggingNodeConfiguration()
        {
        }

        public LoggingNodeConfiguration(
            bool includeEvaluationMetaprojects,
            bool includeEvaluationProfiles,
            bool includeEvaluationPropertiesAndItems,
            bool includeTaskInputs)
        {
            _includeEvaluationMetaprojects = includeEvaluationMetaprojects;
            _includeEvaluationProfiles = includeEvaluationProfiles;
            _includeEvaluationPropertiesAndItems = includeEvaluationPropertiesAndItems;
            _includeTaskInputs = includeTaskInputs;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _includeEvaluationMetaprojects);
            translator.Translate(ref _includeEvaluationProfiles);
            translator.Translate(ref _includeEvaluationPropertiesAndItems);
            translator.Translate(ref _includeTaskInputs);
        }
    }
}
