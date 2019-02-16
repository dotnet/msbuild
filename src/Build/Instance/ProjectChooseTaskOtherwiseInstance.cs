using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Execution
{
    public class ProjectChooseTaskOtherwiseInstance : ITranslatable
    {
        private ElementLocation _location;
        private IList<ProjectTargetInstanceChild> _children;

        internal ProjectChooseTaskOtherwiseInstance(ElementLocation location, IList<ProjectTargetInstanceChild> children)
        {
            ErrorUtilities.VerifyThrowInternalNull(location, nameof(location));
            ErrorUtilities.VerifyThrowInternalNull(children, nameof(children));
            
            _location = location;
            _children = children;
        }

        private ProjectChooseTaskOtherwiseInstance()
        {

        }

        /// <summary>
        /// Cloning constructor
        /// </summary>
        private ProjectChooseTaskOtherwiseInstance(ProjectChooseTaskOtherwiseInstance that)
        {
            // All fields are immutable
            _children = that._children;
        }

        public ElementLocation Location
        {
            [DebuggerStepThrough]
            get => _location;
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
        internal ProjectChooseTaskOtherwiseInstance DeepClone()
        {
            return new ProjectChooseTaskOtherwiseInstance(this);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _children, ProjectTargetInstanceChild.FactoryForDeserialization, count => new List<ProjectTargetInstanceChild>());
        }

        internal static ProjectChooseTaskOtherwiseInstance FactoryForDeserialization(ITranslator translator)
        {
            var instance = new ProjectChooseTaskOtherwiseInstance();
            ((ITranslatable)instance).Translate(translator);

            return instance;
        }
    }
}
