using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an unevaluated Choose under a target.
    /// </summary>
    public class ProjectChooseTaskInstance : ProjectTargetInstanceChild, ITranslatable
    {
        private ElementLocation _location;
        private List<ProjectChooseTaskWhenInstance> _whenInstances;
        private ProjectChooseTaskOtherwiseInstance _otherwise;

        internal ProjectChooseTaskInstance(ElementLocation location, List<ProjectChooseTaskWhenInstance> whenInstances, ProjectChooseTaskOtherwiseInstance otherwise)
        {
            ErrorUtilities.VerifyThrowInternalNull(location, nameof(location));
            ErrorUtilities.VerifyThrowInternalNull(whenInstances, nameof(whenInstances));
            
            _location = location;
            _whenInstances = whenInstances;
            _otherwise = otherwise;
        }

        private ProjectChooseTaskInstance()
        {
        }

        /// <summary>
        /// Cloning constructor
        /// </summary>
        private ProjectChooseTaskInstance(ProjectChooseTaskInstance that)
        {
            // All members are immutable
            _whenInstances = that._whenInstances;
            _otherwise = that._otherwise;
        }

        public override string Condition
        {
            [DebuggerStepThrough]
            get => string.Empty; // Choose does not allow conditions
        }

        public override ElementLocation Location
        {
            [DebuggerStepThrough]
            get => _location;
        }

        public override ElementLocation ConditionLocation
        {
            [DebuggerStepThrough]
            get => null; // Choose does not allow conditions
        }

        public ICollection<ProjectChooseTaskWhenInstance> WhenInstances
        {
            [DebuggerStepThrough]
            get => _whenInstances == null
                ? (ICollection<ProjectChooseTaskWhenInstance>) ReadOnlyEmptyCollection<ProjectChooseTaskWhenInstance>
                    .Instance
                : new ReadOnlyCollection<ProjectChooseTaskWhenInstance>(_whenInstances);
        }

        public ProjectChooseTaskOtherwiseInstance Otherwise
        {
            [DebuggerStepThrough]
            get => _otherwise;
        }

        /// <summary>
        /// Deep clone
        /// </summary>
        internal ProjectChooseTaskInstance DeepClone()
        {
            return new ProjectChooseTaskInstance(this);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var typeName = this.GetType().FullName;
                translator.Translate(ref typeName);
            }
            
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _whenInstances, ProjectChooseTaskWhenInstance.FactoryForDeserialization);
            translator.Translate(ref _otherwise, ProjectChooseTaskOtherwiseInstance.FactoryForDeserialization);
        }
    }
}
