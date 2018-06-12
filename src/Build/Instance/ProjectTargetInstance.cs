// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents a target for build purposes.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using ObjectModel = System.Collections.ObjectModel;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps a target element
    /// </summary>
    /// <remarks>
    /// This is an immutable class.
    /// </remarks>
    [DebuggerDisplay("Name={_name} Count={_children.Count} Condition={_condition} Inputs={_inputs} Outputs={_outputs} DependsOnTargets={_dependsOnTargets}")]
    public sealed class ProjectTargetInstance : IImmutable, IKeyed, INodePacketTranslatable
    {
        /// <summary>
        /// Name of the target
        /// </summary>
        private string _name;

        /// <summary>
        /// Condition on the target. 
        /// Evaluated during the build.
        /// </summary>
        private string _condition;

        /// <summary>
        /// Inputs on the target
        /// </summary>
        private string _inputs;

        /// <summary>
        /// Outputs on the target
        /// </summary>
        private string _outputs;

        /// <summary>
        /// Return values on the target. 
        /// </summary>
        private string _returns;

        /// <summary>
        /// Semicolon separated list of targets it depends on
        /// </summary>
        private string _dependsOnTargets;

        /// <summary>
        /// Condition for whether to trim duplicate outputs
        /// </summary>
        private string _keepDuplicateOutputs;

        /// <summary>
        /// Child entries of the target which refer to OnError targets
        /// </summary>
        private ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance> _onErrorChildren;

        /// <summary>
        /// Whether the project file that this target lives in has at least one target
        /// with a Returns attribute on it.  If so, the default behaviour for all targets
        /// in the file without Returns attributes changes from returning the Outputs, to 
        /// returning nothing. 
        /// </summary>
        private bool _parentProjectSupportsReturnsAttribute;

        /// <summary>
        /// Location of this element
        /// </summary>
        private ElementLocation _location;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private ElementLocation _conditionLocation;

        /// <summary>
        /// Location of the inputs attribute, if any
        /// </summary>
        private ElementLocation _inputsLocation;

        /// <summary>
        /// Location of the outputs attribute, if any
        /// </summary>
        private ElementLocation _outputsLocation;

        /// <summary>
        /// Location of the returns attribute, if any
        /// </summary>
        private ElementLocation _returnsLocation;

        /// <summary>
        /// Location of KeepDuplicateOutputs attribute, if any
        /// </summary>
        private ElementLocation _keepDuplicateOutputsLocation;

        /// <summary>
        /// Location of the DependsOnTargets attribute ,if any
        /// </summary>
        private ElementLocation _dependsOnTargetsLocation;

        /// <summary>
        /// Location of the BeforeTargets attribute ,if any
        /// </summary>
        private ElementLocation _beforeTargetsLocation;

        /// <summary>
        /// Location of the AfterTargets attribute ,if any
        /// </summary>
        private ElementLocation _afterTargetsLocation;

        /// <summary>
        /// Child tasks below the target (both regular tasks and "intrinsic tasks" like ItemGroup and PropertyGroup).
        /// This is a read-only list unless the instance has been modified using AddTask.
        /// </summary>
        private IList<ProjectTargetInstanceChild> _children;

        /// <summary>
        /// Constructor called by Evaluator.
        /// All parameters are in the unevaluated state.
        /// All location parameters may be null if not applicable, except for the main location parameter.
        /// </summary>
        internal ProjectTargetInstance
            (
            string name,
            string condition,
            string inputs,
            string outputs,
            string returns,
            string keepDuplicateOutputs,
            string dependsOnTargets,
            ElementLocation location,
            ElementLocation conditionLocation,
            ElementLocation inputsLocation,
            ElementLocation outputsLocation,
            ElementLocation returnsLocation,
            ElementLocation keepDuplicateOutputsLocation,
            ElementLocation dependsOnTargetsLocation,
            ElementLocation beforeTargetsLocation,
            ElementLocation afterTargetsLocation,
            ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild> children,
            ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance> onErrorChildren,
            bool parentProjectSupportsReturnsAttribute
            )
        {
            ErrorUtilities.VerifyThrowInternalLength(name, "name");
            ErrorUtilities.VerifyThrowInternalNull(condition, "condition");
            ErrorUtilities.VerifyThrowInternalNull(inputs, "inputs");
            ErrorUtilities.VerifyThrowInternalNull(outputs, "outputs");
            ErrorUtilities.VerifyThrowInternalNull(keepDuplicateOutputs, "keepDuplicateOutputs");
            ErrorUtilities.VerifyThrowInternalNull(dependsOnTargets, "dependsOnTargets");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");
            ErrorUtilities.VerifyThrowInternalNull(children, "children");
            ErrorUtilities.VerifyThrowInternalNull(onErrorChildren, "onErrorChildren");

            _name = name;
            _condition = condition;
            _inputs = inputs;
            _outputs = outputs;
            _returns = returns;
            _keepDuplicateOutputs = keepDuplicateOutputs;
            _dependsOnTargets = dependsOnTargets;
            _location = location;
            _conditionLocation = conditionLocation;
            _inputsLocation = inputsLocation;
            _outputsLocation = outputsLocation;
            _returnsLocation = returnsLocation;
            _keepDuplicateOutputsLocation = keepDuplicateOutputsLocation;
            _dependsOnTargetsLocation = dependsOnTargetsLocation;
            _beforeTargetsLocation = beforeTargetsLocation;
            _afterTargetsLocation = afterTargetsLocation;
            _children = children;
            _onErrorChildren = onErrorChildren;
            _parentProjectSupportsReturnsAttribute = parentProjectSupportsReturnsAttribute;
        }

        private ProjectTargetInstance()
        {
        }

        /// <summary>
        /// Name of the target
        /// </summary>
        public string Name
        {
            [DebuggerStepThrough]
            get
            { return _name; }
        }

        /// <summary>
        /// Unevaluated condition on the task.
        /// May be empty string.
        /// </summary>
        public string Condition
        {
            [DebuggerStepThrough]
            get
            { return _condition; }
        }

        /// <summary>
        /// Unevaluated inputs on the target element.
        /// May be empty string.
        /// </summary>
        public string Inputs
        {
            [DebuggerStepThrough]
            get
            { return _inputs; }
        }

        /// <summary>
        /// Unevaluated outputs on the target element
        /// May be empty string.
        /// </summary>
        public string Outputs
        {
            [DebuggerStepThrough]
            get
            { return _outputs; }
        }

        /// <summary>
        /// Unevaluated return values on the target element
        /// May be empty string or null, if no return value is specified.
        /// </summary>
        public string Returns
        {
            [DebuggerStepThrough]
            get
            { return _returns; }
        }

        /// <summary>
        /// Unevaluated condition on which we will trim duplicate outputs from the target outputs
        /// May be empty string.
        /// </summary>
        public string KeepDuplicateOutputs
        {
            [DebuggerStepThrough]
            get
            { return _keepDuplicateOutputs; }
        }

        /// <summary>
        /// Unevaluated semicolon separated list of targets it depends on.
        /// May be empty string.
        /// </summary>
        public string DependsOnTargets
        {
            [DebuggerStepThrough]
            get
            { return _dependsOnTargets; }
        }

        /// <summary>
        /// Children below the target. The build iterates through this to get each task to execute.
        /// This is an ordered collection.
        /// This is a read-only list; the ProjectTargetInstance class is immutable.
        /// This collection does not contain the OnError target references.
        /// </summary>
        public IList<ProjectTargetInstanceChild> Children
        {
            [DebuggerStepThrough]
            get
            { return _children; }
        }

        /// <summary>
        /// The children below the target which refer to OnError targets.
        /// This is an ordered collection.
        /// This is a read-only list; the ProjectTargetInstance class is immutable.
        /// </summary>
        public IList<ProjectOnErrorInstance> OnErrorChildren
        {
            [DebuggerStepThrough]
            get
            { return _onErrorChildren; }
        }

        /// <summary>
        /// Just the tasks below this target, if any.
        /// Other kinds of children are not included.
        /// </summary>
        public ICollection<ProjectTaskInstance> Tasks
        {
            get
            {
                return new ReadOnlyCollection<ProjectTaskInstance>(Children.OfType<ProjectTaskInstance>());
            }
        }

        /// <summary>
        /// Full path to the file from which this target originated.
        /// If it originated in a project that was not loaded and has never been 
        /// given a path, returns an empty string.
        /// </summary>
        public string FullPath
        {
            get { return _location.File; }
        }

        /// <summary>
        /// Location of the original element
        /// </summary>
        public ElementLocation Location
        {
            [DebuggerStepThrough]
            get
            { return _location; }
        }

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        public ElementLocation ConditionLocation
        {
            [DebuggerStepThrough]
            get
            { return _conditionLocation; }
        }

        /// <summary>
        /// Location of the inputs
        /// </summary>
        public ElementLocation InputsLocation
        {
            [DebuggerStepThrough]
            get
            { return _inputsLocation; }
        }

        /// <summary>
        /// Location of the outputs
        /// </summary>
        public ElementLocation OutputsLocation
        {
            [DebuggerStepThrough]
            get
            { return _outputsLocation; }
        }

        /// <summary>
        /// Location of the returns
        /// </summary>
        public ElementLocation ReturnsLocation
        {
            [DebuggerStepThrough]
            get
            { return _returnsLocation; }
        }

        /// <summary>
        /// Location of the KeepDuplicatOutputs attribute
        /// </summary>
        public ElementLocation KeepDuplicateOutputsLocation
        {
            [DebuggerStepThrough]
            get
            { return _keepDuplicateOutputsLocation; }
        }

        /// <summary>
        /// Location of the dependsOnTargets
        /// </summary>
        public ElementLocation DependsOnTargetsLocation
        {
            [DebuggerStepThrough]
            get
            { return _dependsOnTargetsLocation; }
        }

        /// <summary>
        /// Location of the beforeTargets
        /// </summary>
        public ElementLocation BeforeTargetsLocation
        {
            [DebuggerStepThrough]
            get
            { return _beforeTargetsLocation; }
        }

        /// <summary>
        /// Location of the afterTargets
        /// </summary>
        public ElementLocation AfterTargetsLocation
        {
            [DebuggerStepThrough]
            get
            { return _afterTargetsLocation; }
        }

        /// <summary>
        /// Implementation of IKeyed exposing the target name
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IKeyed.Key
        {
            [DebuggerStepThrough]
            get
            { return Name; }
        }

        /// <summary>
        /// Whether the project file that this target lives in has at least one target
        /// with a Returns attribute on it.  If so, the default behaviour for all targets
        /// in the file without Returns attributes changes from returning the Outputs, to 
        /// returning nothing. 
        /// </summary>
        internal bool ParentProjectSupportsReturnsAttribute
        {
            [DebuggerStepThrough]
            get
            { return _parentProjectSupportsReturnsAttribute; }
        }

        /// <summary>
        /// Creates a ProjectTargetElement representing this instance.  Attaches it to the specified root element.
        /// </summary>
        /// <param name="rootElement">The root element to which the new element will belong.</param>
        /// <returns>The new element.</returns>
        internal ProjectTargetElement ToProjectTargetElement(ProjectRootElement rootElement)
        {
            ProjectTargetElement target = rootElement.CreateTargetElement(Name);
            rootElement.AppendChild(target);

            target.Condition = Condition;
            target.DependsOnTargets = DependsOnTargets;
            target.Inputs = Inputs;
            target.Outputs = Outputs;
            target.Returns = Returns;

            foreach (ProjectTaskInstance taskInstance in Tasks)
            {
                ProjectTaskElement taskElement = target.AddTask(taskInstance.Name);
                taskElement.Condition = taskInstance.Condition;
                taskElement.ContinueOnError = taskInstance.ContinueOnError;
                taskElement.MSBuildArchitecture = taskInstance.MSBuildArchitecture;
                taskElement.MSBuildRuntime = taskInstance.MSBuildRuntime;

                foreach (KeyValuePair<string, string> taskParameterEntry in taskInstance.Parameters)
                {
                    taskElement.SetParameter(taskParameterEntry.Key, taskParameterEntry.Value);
                }

                foreach (ProjectTaskInstanceChild outputInstance in taskInstance.Outputs)
                {
                    if (outputInstance is ProjectTaskOutputItemInstance)
                    {
                        ProjectTaskOutputItemInstance outputItemInstance = outputInstance as ProjectTaskOutputItemInstance;
                        taskElement.AddOutputItem(outputItemInstance.TaskParameter, outputItemInstance.ItemType, outputItemInstance.Condition);
                    }
                    else if (outputInstance is ProjectTaskOutputPropertyInstance)
                    {
                        ProjectTaskOutputPropertyInstance outputPropertyInstance = outputInstance as ProjectTaskOutputPropertyInstance;
                        taskElement.AddOutputItem(outputPropertyInstance.TaskParameter, outputPropertyInstance.PropertyName, outputPropertyInstance.Condition);
                    }
                }
            }

            return target;
        }

        /// <summary> Adds new child instance. </summary>
        /// <param name="projectTargetInstanceChild"> Child instance. </param>
        internal void AddProjectTargetInstanceChild(ProjectTargetInstanceChild projectTargetInstanceChild)
        {
            if (!(_children is List<ProjectTargetInstanceChild>))
            {
                _children = new List<ProjectTargetInstanceChild>(_children);
            }

            _children.Add(projectTargetInstanceChild);
        }

        /// <summary>
        /// Creates a new task and adds it to the end of the list of tasks.
        /// </summary>
        /// <param name="taskName">The name of the task to create.</param>
        /// <param name="condition">The task's condition.</param>
        /// <param name="continueOnError">The continue on error flag.</param>
        /// <returns>The new task instance.</returns>
        internal ProjectTaskInstance AddTask(string taskName, string condition, string continueOnError)
        {
            ProjectTaskInstance task = AddTask(taskName, condition, continueOnError, String.Empty, String.Empty);
            return task;
        }

        /// <summary>
        /// Creates a new task and adds it to the end of the list of tasks.
        /// </summary>
        /// <param name="taskName">The name of the task to create.</param>
        /// <param name="condition">The task's condition.</param>
        /// <param name="continueOnError">The continue on error flag.</param>
        /// <param name="msbuildRuntime">The MSBuild runtime.</param>
        /// <param name="msbuildArchitecture">The MSBuild architecture.</param>
        /// <returns>The new task instance.</returns>
        internal ProjectTaskInstance AddTask(string taskName, string condition, string continueOnError, string msbuildRuntime, string msbuildArchitecture)
        {
            ErrorUtilities.VerifyThrowInternalLength(taskName, "taskName");
            ProjectTaskInstance task = new ProjectTaskInstance(taskName, _location, condition ?? String.Empty, continueOnError ?? String.Empty, msbuildRuntime ?? String.Empty, msbuildArchitecture ?? String.Empty);
            this.AddProjectTargetInstanceChild(task);
            return task;
        }

        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _name);
            translator.Translate(ref _condition);
            translator.Translate(ref _inputs);
            translator.Translate(ref _outputs);
            translator.Translate(ref _returns);
            translator.Translate(ref _keepDuplicateOutputs);
            translator.Translate(ref _dependsOnTargets);
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _conditionLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _inputsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _outputsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _returnsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _keepDuplicateOutputsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _dependsOnTargetsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _beforeTargetsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _afterTargetsLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _parentProjectSupportsReturnsAttribute);

            var children = _children;
            translator.Translate(ref children, ProjectTargetInstanceChild.FactoryForDeserialization, count => new List<ProjectTargetInstanceChild>(count));

            IList<ProjectOnErrorInstance> onErrorChildren = _onErrorChildren;
            translator.Translate(ref onErrorChildren, ProjectOnErrorInstance.FactoryForDeserialization, count => new List<ProjectOnErrorInstance>(count));

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _children = new ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild>(children);
                _onErrorChildren = new ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance>(onErrorChildren);
            }
        }

        internal static ProjectTargetInstance FactoryForDeserialization(INodePacketTranslator translator)
        {
            var instance = new ProjectTargetInstance();
            var translatable = (INodePacketTranslatable) instance;

            translatable.Translate(translator);

            return instance;
        }
    }
}
