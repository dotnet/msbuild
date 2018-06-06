// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectTaskElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectTaskElement represents the Task element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("{Name} Condition={Condition} ContinueOnError={ContinueOnError} MSBuildRuntime={MSBuildRuntime} MSBuildArchitecture={MSBuildArchitecture} #Outputs={Count}")]
    public class ProjectTaskElement : ProjectElementContainer
    {
        /// <summary>
        /// The parameters (excepting condition and continue-on-error)
        /// </summary>
        private CopyOnWriteDictionary<string, Tuple<string, ElementLocation>> _parameters;

        /// <summary>
        /// Protection for the parameters cache
        /// </summary>
        private Object _locker = new Object();

        /// <summary>
        /// Initialize a parented ProjectTaskElement
        /// </summary>
        internal ProjectTaskElement(XmlElementWithLocation xmlElement, ProjectTargetElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectTaskElement
        /// </summary>
        private ProjectTaskElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets or sets the continue on error value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string ContinueOnError
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.continueOnError);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.continueOnError, value);
                MarkDirty("Set task ContinueOnError {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the runtime value for the task. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string MSBuildRuntime
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.msbuildRuntime);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.msbuildRuntime, value);
                MarkDirty("Set task MSBuildRuntime {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the architecture value for the task. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string MSBuildArchitecture
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.msbuildArchitecture);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.msbuildArchitecture, value);
                MarkDirty("Set task MSBuildArchitecture {0}", value);
            }
        }

        /// <summary>
        /// Gets the task name
        /// </summary>
        public string Name
        {
            get { return XmlElement.Name; }
        }

        /// <summary>
        /// Gets any output children.
        /// </summary>
        public ICollection<ProjectOutputElement> Outputs
        {
            get
            {
                return new Microsoft.Build.Collections.ReadOnlyCollection<ProjectOutputElement>(Children.OfType<ProjectOutputElement>());
            }
        }

        /// <summary>
        /// Enumerable over the unevaluated parameters on the task.
        /// Attributes with their own properties, such as ContinueOnError, are not included in this collection.
        /// If parameters differ only by case only the last one will be returned. MSBuild uses only this one.
        /// Hosts can still remove the other parameters by using RemoveAllParameters().
        /// </summary>
        public IDictionary<string, string> Parameters
        {
            get
            {
                lock (_locker)
                {
                    EnsureParametersInitialized();

                    Dictionary<string, string> parametersClone = new Dictionary<string, string>(_parameters.Count, StringComparer.OrdinalIgnoreCase);

                    foreach (var entry in _parameters)
                    {
                        parametersClone[entry.Key] = entry.Value.Item1;
                    }

                    return new ReadOnlyDictionary<string, string>(parametersClone);
                }
            }
        }

        /// <summary>
        /// Enumerable over the locations of parameters on the task.
        /// Condition and ContinueOnError, which have their own properties, are not included in this collection.
        /// If parameters differ only by case only the last one will be returned. MSBuild uses only this one.
        /// Hosts can still remove the other parameters by using RemoveAllParameters().
        /// </summary>
        public IEnumerable<KeyValuePair<string, ElementLocation>> ParameterLocations
        {
            get
            {
                lock (_locker)
                {
                    EnsureParametersInitialized();

                    var parameterLocations = new List<KeyValuePair<string, ElementLocation>>();

                    foreach (var entry in _parameters)
                    {
                        parameterLocations.Add(new KeyValuePair<string, ElementLocation>(entry.Key, entry.Value.Item2));
                    }

                    return parameterLocations;
                }
            }
        }

        /// <summary>
        /// Location of the "ContinueOnError" attribute on this element, if any.
        /// If there is no such attribute, returns null;
        /// </summary>
        public ElementLocation ContinueOnErrorLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.continueOnError); }
        }

        /// <summary>
        /// Location of the "MSBuildRuntime" attribute on this element, if any.
        /// If there is no such attribute, returns null;
        /// </summary>
        public ElementLocation MSBuildRuntimeLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.msbuildRuntime); }
        }

        /// <summary>
        /// Location of the "MSBuildArchitecture" attribute on this element, if any.
        /// If there is no such attribute, returns null;
        /// </summary>
        public ElementLocation MSBuildArchitectureLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.msbuildArchitecture); }
        }

        /// <summary>
        /// Retrieves a copy of the parameters as used during evaluation.
        /// </summary>
        internal CopyOnWriteDictionary<string, Tuple<string, ElementLocation>> ParametersForEvaluation
        {
            get
            {
                lock (_locker)
                {
                    EnsureParametersInitialized();

                    return _parameters.Clone(); // copy on write!
                }
            }
        }

        /// <summary>
        /// Convenience method to add an Output Item to this task. 
        /// Adds after the last child. 
        /// </summary>
        public ProjectOutputElement AddOutputItem(string taskParameter, string itemType)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskParameter, "taskParameter");
            ErrorUtilities.VerifyThrowArgumentLength(itemType, "itemType");

            return AddOutputItem(taskParameter, itemType, null);
        }

        /// <summary>
        /// Convenience method to add a conditioned Output Item to this task. 
        /// Adds after the last child. 
        /// </summary>
        public ProjectOutputElement AddOutputItem(string taskParameter, string itemType, string condition)
        {
            ProjectOutputElement outputItem = ContainingProject.CreateOutputElement(taskParameter, itemType, null);

            if (condition != null)
            {
                outputItem.Condition = condition;
            }

            AppendChild(outputItem);

            return outputItem;
        }

        /// <summary>
        /// Convenience method to add an Output Property to this task. 
        /// Adds after the last child. 
        /// </summary>
        public ProjectOutputElement AddOutputProperty(string taskParameter, string propertyName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskParameter, "taskParameter");
            ErrorUtilities.VerifyThrowArgumentLength(propertyName, "propertyName");

            return AddOutputProperty(taskParameter, propertyName, null);
        }

        /// <summary>
        /// Convenience method to add a conditioned Output Property to this task. 
        /// Adds after the last child. 
        /// </summary>
        public ProjectOutputElement AddOutputProperty(string taskParameter, string propertyName, string condition)
        {
            ProjectOutputElement outputProperty = ContainingProject.CreateOutputElement(taskParameter, null, propertyName);

            if (condition != null)
            {
                outputProperty.Condition = condition;
            }

            AppendChild(outputProperty);

            return outputProperty;
        }

        /// <summary>
        /// Gets the value of the parameter with the specified name,
        /// or empty string if it is not present.
        /// </summary>
        public string GetParameter(string name)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentLength(name, "name");

                EnsureParametersInitialized();

                Tuple<string, ElementLocation> parameter;
                if (_parameters.TryGetValue(name, out parameter))
                {
                    return parameter.Item1;
                }

                return String.Empty;
            }
        }

        /// <summary>
        /// Adds (or modifies the value of) a parameter on this task
        /// </summary>
        public void SetParameter(string name, string unevaluatedValue)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentLength(name, "name");
                ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, "unevaluatedValue");
                ErrorUtilities.VerifyThrowArgument(!XMakeAttributes.IsSpecialTaskAttribute(name), "CannotAccessKnownAttributes", name);

                _parameters = null;
                XmlElement.SetAttribute(name, unevaluatedValue);
                MarkDirty("Set task parameter {0}", name);
            }
        }

        /// <summary>
        /// Removes any parameter on this task with the specified name.
        /// If there is no such parameter, does nothing.
        /// </summary>
        public void RemoveParameter(string name)
        {
            lock (_locker)
            {
                _parameters = null;
                XmlElement.RemoveAttribute(name);
                MarkDirty("Remove task parameter {0}", name);
            }
        }

        /// <summary>
        /// Removes all parameters from the task.
        /// Does not remove any "special" parameters: ContinueOnError, Condition, etc.
        /// </summary>
        public void RemoveAllParameters()
        {
            lock (_locker)
            {
                _parameters = null;
                foreach (XmlAttribute attribute in XmlElement.Attributes)
                {
                    if (!XMakeAttributes.IsSpecialTaskAttribute(attribute.Name))
                    {
                        XmlElement.RemoveAttributeNode(attribute);
                    }
                }

                MarkDirty("Remove all task parameters on {0}", Name);
            }
        }

        /// <inheritdoc />
        public override void CopyFrom(ProjectElement element)
        {
            base.CopyFrom(element);

            // Clear caching fields
            _parameters = null;
        }

        /// <summary>
        /// Creates an unparented ProjectTaskElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to the XmlDocument in the appropriate location.
        /// </summary>
        /// <remarks>
        /// Any legal XML element name is allowed. We can't easily verify if the name is a legal XML element name,
        /// so this will specifically throw XmlException if it isn't.
        /// </remarks>
        internal static ProjectTaskElement CreateDisconnected(string name, ProjectRootElement containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");

            XmlElementWithLocation element = containingProject.CreateElement(name);

            return new ProjectTaskElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectTargetElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateTaskElement(this.Name);
        }

        /// <summary>
        /// Initialize parameters cache.
        /// Must be called within the lock.
        /// </summary>
        private void EnsureParametersInitialized()
        {
            if (_parameters == null)
            {
                _parameters = new CopyOnWriteDictionary<string, Tuple<string, ElementLocation>>(XmlElement.Attributes.Count, StringComparer.OrdinalIgnoreCase);

                foreach (XmlAttributeWithLocation attribute in XmlElement.Attributes)
                {
                    if (!XMakeAttributes.IsSpecialTaskAttribute(attribute.Name))
                    {
                        // By pulling off and caching the Location early here, it becomes frozen for the life of this object.
                        // That means that if the name of the file is changed after first load (possibly from null) it will
                        // remain the old value here. Correctly, this should cache the attribute not the location. Fixing 
                        // that will need profiling, though, as this cache was added for performance.
                        _parameters[attribute.Name] = new Tuple<string, ElementLocation>(attribute.Value, attribute.Location);
                    }
                }
            }
        }
    }
}
