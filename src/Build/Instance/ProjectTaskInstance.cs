// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using System.Diagnostics;
using System;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps a task element
    /// </summary>
    /// <remarks>
    /// This is an immutable class
    /// </remarks>
    [DebuggerDisplay("Name={_name} Condition={_condition} ContinueOnError={_continueOnError} MSBuildRuntime={MSBuildRuntime} MSBuildArchitecture={MSBuildArchitecture} #Parameters={_parameters.Count} #Outputs={_outputs.Count}")]
    public sealed class ProjectTaskInstance : ProjectTargetInstanceChild, ITranslatable
    {
        /// <summary>
        /// Name of the task, possibly qualified, as it appears in the project
        /// </summary>
        private string _name;

        /// <summary>
        /// Condition on the task, if any
        /// May be empty string
        /// </summary>
        private string _condition;

        /// <summary>
        /// Continue on error on the task, if any
        /// May be empty string
        /// </summary>
        private string _continueOnError;

        /// <summary>
        /// Runtime on the task, if any
        /// May be empty string
        /// </summary>
        private string _msbuildRuntime;

        /// <summary>
        /// Architecture on the task, if any
        /// May be empty string
        /// </summary>
        private string _msbuildArchitecture;

        /// <summary>
        /// Unordered set of task parameter names and unevaluated values.
        /// This is a dead, read-only collection.
        /// </summary>
        private CopyOnWriteDictionary<string, (string, ElementLocation)> _parameters;

        /// <summary>
        /// Output properties and items below this task. This is an ordered collection
        /// as one may depend on another.
        /// This is a dead, read-only collection.
        /// </summary>
        private List<ProjectTaskInstanceChild> _outputs;

        /// <summary>
        /// Location of this element
        /// </summary>
        private ElementLocation _location;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private ElementLocation _conditionLocation;

        /// <summary>
        /// Location of the continueOnError attribute, if any
        /// </summary>
        private ElementLocation _continueOnErrorLocation;

        /// <summary>
        /// Location of the MSBuildRuntime attribute, if any
        /// </summary>
        private ElementLocation _msbuildRuntimeLocation;

        /// <summary>
        /// Location of the MSBuildArchitecture attribute, if any
        /// </summary>
        private ElementLocation _msbuildArchitectureLocation;

        /// <summary>
        /// Constructor called by Evaluator.
        /// All parameters are in the unevaluated state.
        /// Locations other than the main location may be null.
        /// </summary>
        internal ProjectTaskInstance
            (
            ProjectTaskElement element,
            IList<ProjectTaskInstanceChild> outputs
            )
        {
            ErrorUtilities.VerifyThrowInternalNull(element, "element");
            ErrorUtilities.VerifyThrowInternalNull(outputs, "outputs");

            // These are all immutable
            _name = element.Name;
            _condition = element.Condition;
            _continueOnError = element.ContinueOnError;
            _msbuildArchitecture = element.MSBuildArchitecture;
            _msbuildRuntime = element.MSBuildRuntime;
            _location = element.Location;
            _conditionLocation = element.ConditionLocation;
            _continueOnErrorLocation = element.ContinueOnErrorLocation;
            _msbuildRuntimeLocation = element.MSBuildRuntimeLocation;
            _msbuildArchitectureLocation = element.MSBuildArchitectureLocation;
            _parameters = element.ParametersForEvaluation;
            _outputs = new List<ProjectTaskInstanceChild>(outputs);
        }

        /// <summary>
        ///     Creates a new task instance directly.  Used for generating instances on-the-fly.
        /// </summary>
        /// <param name="name">The task name.</param>
        /// <param name="location">The location for this task.</param>
        /// <param name="condition">The unevaluated condition.</param>
        /// <param name="continueOnError">The unevaluated continue on error.</param>
        /// <param name="msbuildRuntime">The MSBuild runtime.</param>
        /// <param name="msbuildArchitecture">The MSBuild architecture.</param>
        internal ProjectTaskInstance(
            string name,
            ElementLocation location,
            string condition,
            string continueOnError,
            string msbuildRuntime,
            string msbuildArchitecture
        ) : this(
            name,
            condition,
            continueOnError,
            msbuildRuntime,
            msbuildArchitecture,
            new CopyOnWriteDictionary<string, (string, ElementLocation)>(8, StringComparer.OrdinalIgnoreCase),
            new List<ProjectTaskInstanceChild>(),
            location,
            condition == string.Empty ? null : ElementLocation.EmptyLocation,
            continueOnError == string.Empty ? null : ElementLocation.EmptyLocation,
            msbuildRuntime == string.Empty ? null : ElementLocation.EmptyLocation,
            msbuildArchitecture == string.Empty ? null : ElementLocation.EmptyLocation)
        {
        }

        internal ProjectTaskInstance
            (
            string name,
            string condition,
            string continueOnError,
            string msbuildRuntime,
            string msbuildArchitecture,
            CopyOnWriteDictionary<string, (string, ElementLocation)> parameters,
            List<ProjectTaskInstanceChild> outputs,
            ElementLocation location,
            ElementLocation conditionLocation,
            ElementLocation continueOnErrorElementLocation,
            ElementLocation msbuildRuntimeLocation,
            ElementLocation msbuildArchitectureLocation)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");
            ErrorUtilities.VerifyThrowArgumentNull(condition, "condition");
            ErrorUtilities.VerifyThrowArgumentNull(continueOnError, "continueOnError");

            _name = name;
            _condition = condition;
            _continueOnError = continueOnError;
            _msbuildRuntime = msbuildRuntime;
            _msbuildArchitecture = msbuildArchitecture;
            _location = location;
            _conditionLocation = conditionLocation;
            _continueOnErrorLocation = continueOnErrorElementLocation;
            _msbuildArchitectureLocation = msbuildArchitectureLocation;
            _msbuildRuntimeLocation = msbuildRuntimeLocation;
            _parameters = parameters;
            _outputs = outputs;
        }

        private ProjectTaskInstance()
        {
        }

        /// <summary>
        /// Name of the task, possibly qualified, as it appears in the project
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Unevaluated condition on the task
        /// May be empty string.
        /// </summary>
        public override string Condition
        {
            get { return _condition; }
        }

        /// <summary>
        /// Unevaluated ContinueOnError on the task.
        /// May be empty string.
        /// </summary>
        public string ContinueOnError
        {
            get { return _continueOnError; }
        }

        /// <summary>
        /// Unevaluated MSBuildRuntime on the task.
        /// May be empty string.
        /// </summary>
        public string MSBuildRuntime
        {
            get { return _msbuildRuntime; }
        }

        /// <summary>
        /// Unevaluated MSBuildArchitecture on the task.
        /// May be empty string.
        /// </summary>
        public string MSBuildArchitecture
        {
            get { return _msbuildArchitecture; }
        }

        /// <summary>
        /// Read-only dead unordered set of task parameter names and unevaluated values.
        /// Condition and ContinueOnError, which have their own properties, are not included in this collection.
        /// </summary>
        public IDictionary<string, string> Parameters
        {
            get
            {
                Dictionary<string, string> filteredParameters = new Dictionary<string, string>(_parameters.Count, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, (string, ElementLocation)> parameter in _parameters)
                {
                    filteredParameters[parameter.Key] = parameter.Value.Item1;
                }

                return filteredParameters;
            }
        }

        internal IDictionary<string, (string, ElementLocation)> TestGetParameters => _parameters;

        /// <summary>
        /// Ordered set of output property and item objects.
        /// This is a read-only dead collection.
        /// </summary>
        public IList<ProjectTaskInstanceChild> Outputs
        {
            get { return _outputs; }
        }

        /// <summary>
        /// Location of the ContinueOnError attribute, if any
        /// </summary>
        public ElementLocation ContinueOnErrorLocation
        {
            get { return _continueOnErrorLocation; }
        }

        /// <summary>
        /// Location of the MSBuildRuntime attribute, if any
        /// </summary>
        public ElementLocation MSBuildRuntimeLocation
        {
            get { return _msbuildRuntimeLocation; }
        }

        /// <summary>
        /// Location of the MSBuildArchitecture attribute, if any
        /// </summary>
        public ElementLocation MSBuildArchitectureLocation
        {
            get { return _msbuildArchitectureLocation; }
        }

        /// <summary>
        /// Location of the original element
        /// </summary>
        public override ElementLocation Location
        {
            get { return _location; }
        }

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        public override ElementLocation ConditionLocation
        {
            get { return _conditionLocation; }
        }

        /// <summary>
        /// Retrieves the parameters dictionary as used during the build.
        /// </summary>
        internal IDictionary<string, (string, ElementLocation)> ParametersForBuild
        {
            get { return _parameters; }
        }

        /// <summary>
        /// Returns the value of a named parameter, or null if there is no such parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to retrieve.</param>
        /// <returns>The parameter value, or null if it does not exist.</returns>
        internal string GetParameter(string parameterName)
        {
            if (_parameters.TryGetValue(parameterName, out var parameterValue))
            {
                return parameterValue.Item1;
            }

            return null;
        }

        /// <summary>
        /// Sets the unevaluated value for the specified parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to set.</param>
        /// <param name="unevaluatedValue">The unevaluated value for the parameter.</param>
        internal void SetParameter(string parameterName, string unevaluatedValue)
        {
            _parameters[parameterName] = (unevaluatedValue, ElementLocation.EmptyLocation);
        }

        /// <summary>
        /// Adds an output item to the task.
        /// </summary>
        /// <param name="taskOutputParameterName">The name of the parameter on the task which produces the output.</param>
        /// <param name="itemName">The item which will receive the output.</param>
        /// <param name="condition">The condition.</param>
        internal void AddOutputItem(string taskOutputParameterName, string itemName, string condition)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskOutputParameterName, "taskOutputParameterName");
            ErrorUtilities.VerifyThrowArgumentLength(itemName, "itemName");
            _outputs.Add(new ProjectTaskOutputItemInstance(itemName, taskOutputParameterName, condition ?? String.Empty, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, condition == null ? null : ElementLocation.EmptyLocation));
        }

        /// <summary>
        /// Adds an output property to the task.
        /// </summary>
        /// <param name="taskOutputParameterName">The name of the parameter on the task which produces the output.</param>
        /// <param name="propertyName">The property which will receive the output.</param>
        /// <param name="condition">The condition.</param>
        internal void AddOutputProperty(string taskOutputParameterName, string propertyName, string condition)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskOutputParameterName, "taskOutputParameterName");
            ErrorUtilities.VerifyThrowArgumentLength(propertyName, "propertyName");
            _outputs.Add(new ProjectTaskOutputPropertyInstance(propertyName, taskOutputParameterName, condition ?? String.Empty, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, condition == null ? null : ElementLocation.EmptyLocation));
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var typeName = this.GetType().FullName;
                translator.Translate(ref typeName);
            }

            translator.Translate(ref _name);
            translator.Translate(ref _condition);
            translator.Translate(ref _continueOnError);
            translator.Translate(ref _msbuildRuntime);
            translator.Translate(ref _msbuildArchitecture);
            translator.Translate(ref _outputs, ProjectTaskInstanceChild.FactoryForDeserialization);
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _conditionLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _continueOnErrorLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _msbuildRuntimeLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _msbuildArchitectureLocation, ElementLocation.FactoryForDeserialization);

            IDictionary<string, (string, ElementLocation)> localParameters = _parameters;
            translator.TranslateDictionary(
                ref localParameters,
                ParametersKeyTranslator,
                ParametersValueTranslator,
                count => new CopyOnWriteDictionary<string, (string, ElementLocation)>(count));

            if (translator.Mode == TranslationDirection.ReadFromStream && localParameters != null)
            {
                _parameters = (CopyOnWriteDictionary<string, (string, ElementLocation)>) localParameters;
            }
        }

        private static void ParametersKeyTranslator(ITranslator translator, ref string key)
        {
            translator.Translate(ref key);
        }

        private static void ParametersValueTranslator(ITranslator translator, ref (string, ElementLocation) value)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var item1 = value.Item1;
                var item2 = value.Item2;

                translator.Translate(ref item1);
                translator.Translate(ref item2, ElementLocation.FactoryForDeserialization);
            }
            else
            {
                var item1 = default(string);
                var item2 = default(ElementLocation);

                translator.Translate(ref item1);
                translator.Translate(ref item2, ElementLocation.FactoryForDeserialization);

                value = (item1, item2);
            }
        }

        internal new static ProjectTaskInstance FactoryForDeserialization(ITranslator translator)
        {
            return translator.FactoryForDeserializingTypeWithName<ProjectTaskInstance>();
        }
    }
}
