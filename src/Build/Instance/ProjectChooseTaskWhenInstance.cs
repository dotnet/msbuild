using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    public class ProjectChooseTaskWhenInstance : ITranslatable
    {
        private string _condition;
        private ElementLocation _location;
        private ElementLocation _conditionLocation;
        private IList<ProjectTargetInstanceChild> _children;

        internal ProjectChooseTaskWhenInstance(string condition, ElementLocation location,
            ElementLocation conditionLocation, IList<ProjectTargetInstanceChild> children)
        {
            ErrorUtilities.VerifyThrowInternalNull(condition, nameof(condition));
            ErrorUtilities.VerifyThrowInternalNull(location, nameof(location));
            ErrorUtilities.VerifyThrowInternalNull(conditionLocation, nameof(conditionLocation));
            ErrorUtilities.VerifyThrowInternalNull(children, nameof(children));

            _condition = condition;
            _location = location;
            _conditionLocation = conditionLocation;
            _children = children;
        }

        private ProjectChooseTaskWhenInstance()
        {

        }

        /// <summary>
        /// Cloning constructor
        /// </summary>
        private ProjectChooseTaskWhenInstance(ProjectChooseTaskWhenInstance that)
        {
            // All fields are immutable
            _condition = that._condition;
            _children = that._children;
        }

        public string Condition
        {
            [DebuggerStepThrough]
            get => _condition;
        }

        public ElementLocation Location
        {
            [DebuggerStepThrough]
            get => _location;
        }

        public ElementLocation ConditionLocation
        {
            [DebuggerStepThrough]
            get => _conditionLocation;
        }

        public ICollection<ProjectTargetInstanceChild> Children
        {
            [DebuggerStepThrough]
            get => _children == null
                ? (ICollection<ProjectTargetInstanceChild>)ReadOnlyEmptyCollection<ProjectTargetInstanceChild>
                    .Instance
                : new ReadOnlyCollection<ProjectTargetInstanceChild>(_children);
        }

        /// <summary>
        /// Deep clone
        /// </summary>
        internal ProjectChooseTaskWhenInstance DeepClone()
        {
            return new ProjectChooseTaskWhenInstance(this);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _condition);
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _conditionLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _children, ProjectTargetInstanceChild.FactoryForDeserialization, count => new List<ProjectTargetInstanceChild>());
        }

        internal static ProjectChooseTaskWhenInstance FactoryForDeserialization(ITranslator translator)
        {
            var instance = new ProjectChooseTaskWhenInstance();
            ((ITranslatable)instance).Translate(translator);

            return instance;
        }
    }
}
