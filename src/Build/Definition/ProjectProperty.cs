// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Wraps a logical property on an item.</summary>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An evaluated design-time property 
    /// </summary>
    [DebuggerDisplay("{Name}={EvaluatedValue} [{UnevaluatedValue}]")]
    public abstract class ProjectProperty : IKeyed, IValued, IProperty, IEquatable<ProjectProperty>
    {
        /// <summary>
        /// Project that this property lives in.
        /// ProjectProperty's always live in a project.
        /// Used to evaluate any updates.
        /// </summary>
        private readonly Project _project;

        /// <summary>
        /// Evaluated value of the property.  Escaped as necessary.
        /// </summary>
        private string _evaluatedValueEscaped;

        /// <summary>
        /// Creates a property.
        /// </summary>
        internal ProjectProperty(Project project, string evaluatedValueEscaped)
        {
            ErrorUtilities.VerifyThrowArgumentNull(project, "project");
            ErrorUtilities.VerifyThrowArgumentNull(evaluatedValueEscaped, "evaluatedValueEscaped");

            _project = project;
            _evaluatedValueEscaped = evaluatedValueEscaped;
        }

        /// <summary>
        /// Name of the property.
        /// Cannot be set.
        /// </summary>
        /// <comment>
        /// If this could be set, it would be necessary to have a callback
        /// so that the containing collections could be updated, as they use the name as 
        /// their key.
        /// </comment>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract string Name
        {
            [DebuggerStepThrough]
            get;
        }

        /// <summary>
        /// Gets the evaluated property value.
        /// Cannot be set directly: only the unevaluated value can be set.
        /// Is never null.
        /// </summary>
        /// <remarks>
        /// Unescaped value of the evaluated property
        /// </remarks>
        public string EvaluatedValue
        {
            [DebuggerStepThrough]
            get
            { return EscapingUtilities.UnescapeAll(_evaluatedValueEscaped); }
        }

        /// <summary>
        /// Gets the evaluated property value.
        /// Cannot be set directly: only the unevaluated value can be set.
        /// Is never null.
        /// </summary>
        /// <remarks>
        /// Evaluated property escaped as necessary
        /// </remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IProperty.EvaluatedValueEscaped
        {
            [DebuggerStepThrough]
            get
            { return _evaluatedValueEscaped; }
        }

        /// <summary>
        /// Gets or sets the unevaluated property value.
        /// Updates the evaluated value in the project, although this is not sure to be correct until re-evaluation.
        /// </summary>
        public abstract string UnevaluatedValue
        {
            [DebuggerStepThrough]
            get;
            set;
        }

        /// <summary>
        /// Whether the property originated from the environment (or the toolset)
        /// </summary>
        public abstract bool IsEnvironmentProperty
        {
            [DebuggerStepThrough]
            get;
        }

        /// <summary>
        /// Whether the property is a global property
        /// </summary>
        public abstract bool IsGlobalProperty
        {
            [DebuggerStepThrough]
            get;
        }

        /// <summary>
        /// Whether the property is a reserved property,
        /// like 'MSBuildProjectFile'.
        /// </summary>
        public abstract bool IsReservedProperty
        {
            [DebuggerStepThrough]
            get;
        }

        /// <summary>
        /// Backing XML property.
        /// Null only if this is a global, environment, or built-in property.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract ProjectPropertyElement Xml
        {
            [DebuggerStepThrough]
            get;
        }

        /// <summary>
        /// Project that this property lives in.
        /// ProjectProperty's always live in a project.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Project Project
        {
            [DebuggerStepThrough]
            get
            { return _project; }
        }

        /// <summary>
        /// Any immediately previous property that was overridden by this one during evaluation.
        /// This would include all properties with the same name that lie above in the logical
        /// project file, and whose conditions evaluated to true.
        /// If there are none above this is null.
        /// If the project has not been reevaluated since the last modification this value may be incorrect.
        /// </summary>
        public abstract ProjectProperty Predecessor
        {
            [DebuggerStepThrough]
            get;
        }

        /// <summary>
        /// If the property originated in an imported file, returns true.
        /// If the property originates from the environment, a global property, or is a built-in property, returns false.
        /// Otherwise returns false.
        /// </summary>
        public abstract bool IsImported
        {
            get;
        }

        /// <summary>
        /// Implementation of IKeyed exposing the property name, so properties
        /// can be put in a dictionary conveniently.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IKeyed.Key
        {
            [DebuggerStepThrough]
            get
            { return Name; }
        }

        /// <summary>
        /// Implementation of IValued
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IValued.EscapedValue
        {
            [DebuggerStepThrough]
            get
            { return _evaluatedValueEscaped; }
        }

        #region IEquatable<ProjectProperty> Members

        /// <summary>
        /// Compares this property to another for equivalence.
        /// </summary>
        /// <param name="other">The other property.</param>
        /// <returns>True if the properties are equivalent, false otherwise.</returns>
        bool IEquatable<ProjectProperty>.Equals(ProjectProperty other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (null == other)
            {
                return false;
            }

            return _project == other._project &&
                   Xml == other.Xml &&
                   _evaluatedValueEscaped == other._evaluatedValueEscaped &&
                   Name == other.Name;
        }

        #endregion

        /// <summary>
        /// Creates a property without backing XML. 
        /// Property MAY BE global, and property MAY HAVE a reserved name (such as "MSBuildProjectDirectory") if indicated.
        /// This is ONLY to be used by the Evaluator (and Project.SetGlobalProperty) and ONLY for Global, Environment, and Built-in properties.
        /// All other properties originate in XML, and should have a backing XML object.
        /// </summary>
        internal static ProjectProperty Create(Project project, string name, string evaluatedValueEscaped, bool isGlobalProperty, bool mayBeReserved)
        {
            return new ProjectPropertyNotXmlBacked(project, name, evaluatedValueEscaped, isGlobalProperty, mayBeReserved);
        }

        /// <summary>
        /// Creates a regular evaluated property, with backing XML.
        /// Called by Project.SetProperty.
        /// Property MAY NOT have reserved name and MAY NOT overwrite a global property.
        /// Predecessor is any immediately previous property that was overridden by this one during evaluation and may be null.
        /// </summary>
        internal static ProjectProperty Create(Project project, ProjectPropertyElement xml, string evaluatedValueEscaped, ProjectProperty predecessor)
        {
            if (predecessor == null)
            {
                return new ProjectPropertyXmlBacked(project, xml, evaluatedValueEscaped);
            }
            else
            {
                return new ProjectPropertyXmlBackedWithPredecessor(project, xml, evaluatedValueEscaped, predecessor);
            }
        }

        /// <summary>
        /// Called ONLY by the project in order to update the evaluated value
        /// after a property set occurring between full evaluations.
        /// </summary>
        /// <remarks>
        /// Method instead of a setter on EvaluatedValue to try to make clear its limited purpose.
        /// </remarks>
        internal void UpdateEvaluatedValue(string evaluatedValueEscaped)
        {
            _evaluatedValueEscaped = evaluatedValueEscaped;
        }

        /// <summary>
        /// Looks for a matching global property.
        /// </summary>
        /// <remarks>
        /// The reason we do this and not just look at project.GlobalProperties is
        /// that when the project is being loaded, the GlobalProperties collection is already populated.  When we do our
        /// evaluation, we may attempt to add some properties, such as environment variables, to the master Properties 
        /// collection.  As GlobalProperties are supposed to override these and thus be added last, we can't check against
        /// the GlobalProperties collection as they are being added.  The correct behavior is to always check against the
        /// collection which is accumulating properties as we go, which is the Properties collection.  Once the project has
        /// been fully populated, this method will also ensure that further properties do not attempt to override global
        /// properties, as those will have the global property flag set.
        /// </remarks>
        /// <param name="project">The project to compare with.</param>
        /// <param name="propertyName">The property name to look up</param>
        /// <returns>True if there is a matching global property, false otherwise.</returns>
        private static bool ProjectHasMatchingGlobalProperty(Project project, string propertyName)
        {
            ProjectProperty property = project.GetProperty(propertyName);
            if (property != null && property.IsGlobalProperty && !project.GlobalPropertiesToTreatAsLocal.Contains(propertyName))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Regular property, originating in an XML node, but with no predecessor (property with same name that it overrode during evaluation)
        /// </summary>
        private class ProjectPropertyXmlBacked : ProjectProperty
        {
            /// <summary>
            /// Backing XML property.
            /// Never null.
            /// </summary>
            private readonly ProjectPropertyElement _xml;

            /// <summary>
            /// Creates a regular evaluated property, with backing XML.
            /// Called by Project.SetProperty.
            /// Property MAY NOT have reserved name and MAY NOT overwrite a global property.
            /// Predecessor is any immediately previous property that was overridden by this one during evaluation and may be null.
            /// </summary>
            internal ProjectPropertyXmlBacked(Project project, ProjectPropertyElement xml, string evaluatedValueEscaped)
                : base(project, evaluatedValueEscaped)
            {
                ErrorUtilities.VerifyThrowArgumentNull(xml, "xml");
                ErrorUtilities.VerifyThrowInvalidOperation(!ProjectHasMatchingGlobalProperty(project, xml.Name), "OM_GlobalProperty", xml.Name);

                _xml = xml;
            }

            /// <summary>
            /// Name of the property.
            /// Cannot be set.
            /// </summary>
            /// <comment>
            /// If this could be set, it would be necessary to have a callback
            /// so that the containing collections could be updated, as they use the name as 
            /// their key.
            /// </comment>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override string Name
            {
                [DebuggerStepThrough]
                get
                { return _xml.Name; }
            }

            /// <summary>
            /// Gets or sets the unevaluated property value.
            /// Updates the evaluated value in the project, although this is not sure to be correct until re-evaluation.
            /// </summary>
            /// <remarks>
            /// The containing project will be dirtied by the XML modification.
            /// If there is no XML backing, the evaluated value returned is the value of the property that has been 
            /// escaped as necessary.
            /// </remarks>
            public override string UnevaluatedValue
            {
                [DebuggerStepThrough]
                get
                {
                    return _xml.Value;
                }

                set
                {
                    Project.VerifyThrowInvalidOperationNotImported(_xml.ContainingProject);
                    ErrorUtilities.VerifyThrowInvalidOperation(_xml.Parent != null && _xml.Parent.Parent != null, "OM_ObjectIsNoLongerActive");

                    _xml.Value = value;

                    _evaluatedValueEscaped = _project.ExpandPropertyValueBestEffortLeaveEscaped(value, _xml.Location);
                }
            }

            /// <summary>
            /// Whether the property originated from the environment (or the toolset)
            /// </summary>
            public override bool IsEnvironmentProperty
            {
                [DebuggerStepThrough]
                get
                { return false; }
            }

            /// <summary>
            /// Whether the property is a global property
            /// </summary>
            public override bool IsGlobalProperty
            {
                [DebuggerStepThrough]
                get
                { return false; }
            }

            /// <summary>
            /// Whether the property is a reserved property,
            /// like 'MSBuildProjectFile'.
            /// </summary>
            public override bool IsReservedProperty
            {
                [DebuggerStepThrough]
                get
                { return false; }
            }

            /// <summary>
            /// Backing XML property.
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override ProjectPropertyElement Xml
            {
                [DebuggerStepThrough]
                get
                { return _xml; }
            }

            /// <summary>
            /// Any immediately previous property that was overridden by this one during evaluation.
            /// This would include all properties with the same name that lie above in the logical
            /// project file, and whose conditions evaluated to true.
            /// In this class this is null.
            /// If the project has not been reevaluated since the last modification this value may be incorrect.
            /// </summary>
            public override ProjectProperty Predecessor
            {
                [DebuggerStepThrough]
                get
                { return null; }
            }

            /// <summary>
            /// If the property originated in an imported file, returns true.
            /// Otherwise returns false.
            /// </summary>
            public override bool IsImported
            {
                get
                {
                    bool isImported = !Object.ReferenceEquals(_xml.ContainingProject, _project.Xml);

                    return isImported;
                }
            }
        }

        /// <summary>
        /// Regular property, originating in an XML node, and with a predecessor (property with same name that was overridden during evaluation)
        /// </summary>
        private class ProjectPropertyXmlBackedWithPredecessor : ProjectPropertyXmlBacked
        {
            /// <summary>
            /// Any immediately previous property that was overridden by this one during evaluation.
            /// This would include all properties with the same name that lie above in the logical
            /// project file, and whose conditions evaluated to true.
            /// If there are none above this is null.
            /// If the project has not been reevaluated since the last modification this value may be incorrect.
            /// </summary>
            private ProjectProperty _predecessor;

            /// <summary>
            /// Creates a regular evaluated property, with backing XML.
            /// Called by Project.SetProperty.
            /// Property MAY NOT have reserved name and MAY NOT overwrite a global property.
            /// Predecessor is any immediately previous property that was overridden by this one during evaluation and may be null.
            /// </summary>
            internal ProjectPropertyXmlBackedWithPredecessor(Project project, ProjectPropertyElement xml, string evaluatedValueEscaped, ProjectProperty predecessor)
                : base(project, xml, evaluatedValueEscaped)
            {
                ErrorUtilities.VerifyThrowArgumentNull(predecessor, "predecessor");

                _predecessor = predecessor;
            }

            /// <summary>
            /// Any immediately previous property that was overridden by this one during evaluation.
            /// This would include all properties with the same name that lie above in the logical
            /// project file, and whose conditions evaluated to true.
            /// If there are none above this is null.
            /// If the project has not been reevaluated since the last modification this value may be incorrect.
            /// </summary>
            public override ProjectProperty Predecessor
            {
                [DebuggerStepThrough]
                get
                { return _predecessor; }
            }
        }

        /// <summary>
        /// Global/environment/toolset properties are the minority;
        /// they don't originate with XML, so we must store their name (instead)
        /// </summary>
        private class ProjectPropertyNotXmlBacked : ProjectProperty
        {
            /// <summary>
            /// Name of the property.
            /// </summary>
            private readonly string _name;

            /// <summary>
            /// Creates a property without backing XML. 
            /// Property MAY BE global, and property MAY HAVE a reserved name (such as "MSBuildProjectDirectory") if indicated.
            /// This is ONLY to be used by the Evaluator (and Project.SetGlobalProperty) and ONLY for Global, Environment, and Built-in properties.
            /// All other properties originate in XML, and should have a backing XML object.
            /// </summary>
            internal ProjectPropertyNotXmlBacked(Project project, string name, string evaluatedValueEscaped, bool isGlobalProperty, bool mayBeReserved)
                : base(project, evaluatedValueEscaped)
            {
                ErrorUtilities.VerifyThrowArgumentLength(name, "name");
                ErrorUtilities.VerifyThrowInvalidOperation(isGlobalProperty || !ProjectHasMatchingGlobalProperty(project, name), "OM_GlobalProperty", name);
                ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(name), "OM_ReservedName", name);
                ErrorUtilities.VerifyThrowArgument(mayBeReserved || !ReservedPropertyNames.IsReservedProperty(name), "OM_ReservedName", name);

                _name = name;
            }

            /// <summary>
            /// Name of the property.
            /// Cannot be set.
            /// </summary>
            /// <comment>
            /// If this could be set, it would be necessary to have a callback
            /// so that the containing collections could be updated, as they use the name as 
            /// their key.
            /// </comment>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override string Name
            {
                [DebuggerStepThrough]
                get
                { return _name; }
            }

            /// <summary>
            /// Gets or sets the unevaluated property value.
            /// Updates the evaluated value in the project, although this is not sure to be correct until re-evaluation.
            /// </summary>
            /// <remarks>
            /// The containing project will be dirtied.
            /// As there is no XML backing, the evaluated value returned is the value of the property that has been 
            /// escaped as necessary.
            /// </remarks>
            public override string UnevaluatedValue
            {
                [DebuggerStepThrough]
                get
                {
                    return ((IProperty)this).EvaluatedValueEscaped;
                }

                set
                {
                    ErrorUtilities.VerifyThrowInvalidOperation(!IsReservedProperty, "OM_ReservedName", _name);
                    ErrorUtilities.VerifyThrowInvalidOperation(!IsGlobalProperty, "OM_GlobalProperty", _name);

                    if (IsEnvironmentProperty)
                    {
                        // Although this is an environment property, the user wants it
                        // to be persisted. So as well as updating this object,
                        // tell the project to add a real persisted property to match.
                        _evaluatedValueEscaped = value;

                        _project.Xml.AddProperty(_name, value);

                        return;
                    }

                    ErrorUtilities.ThrowInternalErrorUnreachable();
                }
            }

            /// <summary>
            /// Whether the property originated from the environment (or the toolset)
            /// </summary>
            public override bool IsEnvironmentProperty
            {
                get { return (!IsGlobalProperty && !IsReservedProperty); }
            }

            /// <summary>
            /// Whether the property is a global property
            /// </summary>
            public override bool IsGlobalProperty
            {
                [DebuggerStepThrough]
                get
                { return _project.GlobalProperties.ContainsKey(Name); }
            }

            /// <summary>
            /// Whether the property is a reserved property,
            /// like 'MSBuildProjectFile'.
            /// </summary>
            public override bool IsReservedProperty
            {
                [DebuggerStepThrough]
                get
                { return ReservedPropertyNames.IsReservedProperty(Name); }
            }

            /// <summary>
            /// Backing XML property.
            /// Null because this is a global, environment, or built-in property.
            /// </summary>
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public override ProjectPropertyElement Xml
            {
                [DebuggerStepThrough]
                get
                { return null; }
            }

            /// <summary>
            /// Any immediately previous property that was overridden by this one during evaluation.
            /// Because these properties are not backed by XML, they cannot have precedessors.
            /// </summary>
            public override ProjectProperty Predecessor
            {
                [DebuggerStepThrough]
                get
                { return null; }
            }

            /// <summary>
            /// Whether the property originated in an imported file.
            /// Because these properties did not originate in an XML file, this always returns null.
            /// </summary>
            public override bool IsImported
            {
                get { return false; }
            }
        }
    }
}
