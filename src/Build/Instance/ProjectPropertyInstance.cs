﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an evaluated property for build purposes.
    /// Added and removed via methods on the ProjectInstance object.
    /// </summary>
    [DebuggerDisplay("{_name}={_escapedValue}")]
    public class ProjectPropertyInstance : IKeyed, IValued, IProperty, IEquatable<ProjectPropertyInstance>, ITranslatable
    {
        /// <summary>
        /// Name of the property
        /// </summary>
        private string _name;

        /// <summary>
        /// Evaluated value: stored escaped. 
        /// </summary>
        private string _escapedValue;

        /// <summary>
        /// Private constructor
        /// </summary>
        private ProjectPropertyInstance(string name, string escapedValue)
        {
            _name = name;
            _escapedValue = escapedValue;
        }

        /// <summary>
        /// Name of the property
        /// </summary>
        /// <remarks>
        /// This cannot be set, as it is used as the key into 
        /// the project's properties table.
        /// </remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Name => _name;

        /// <summary>
        /// Evaluated value of the property.
        /// Setter assumes caller has protected global properties, if necessary
        /// SETTER ASSUMES CALLER ONLY CALLS IF PROJECTINSTANCE IS MUTABLE because it cannot always be verified.
        /// </summary>
        public string EvaluatedValue
        {
            [DebuggerStepThrough]
            get
            {
                return EscapingUtilities.UnescapeAll(_escapedValue);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectInstance.VerifyThrowNotImmutable(IsImmutable);
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(value));
                _escapedValue = EscapingUtilities.Escape(value);
            }
        }

        /// <summary>
        /// Whether this object is immutable.
        /// An immutable object can not be made mutable.
        /// </summary>
        public virtual bool IsImmutable => false;

        /// <summary>
        /// Evaluated value of the property, escaped as necessary.
        /// Setter assumes caller has protected global properties, if necessary.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IProperty.EvaluatedValueEscaped
        {
            get
            {
                if (this is EnvironmentDerivedProjectPropertyInstance envProperty && envProperty.loggingContext?.IsValid == true && !envProperty._loggedEnvProperty && !Traits.LogAllEnvironmentVariables)
                {
                    EnvironmentVariableReadEventArgs args = new(Name, _escapedValue);
                    args.BuildEventContext = envProperty.loggingContext.BuildEventContext;
                    envProperty.loggingContext.LogBuildEvent(args);
                    envProperty._loggedEnvProperty = true;
                }

                return _escapedValue;
            }
        }
        /// <summary>
        /// Implementation of IKeyed exposing the property name
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IKeyed.Key => Name;

        /// <summary>
        /// Implementation of IValued
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IValued.EscapedValue => _escapedValue;

        #region IEquatable<ProjectPropertyInstance> Members

        /// <summary>
        /// Compares this property to another for equivalence.
        /// </summary>
        /// <param name="other">The other property.</param>
        /// <returns>True if the properties are equivalent, false otherwise.</returns>
        bool IEquatable<ProjectPropertyInstance>.Equals(ProjectPropertyInstance other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            // Do not consider mutability for equality comparison
            return _escapedValue == other._escapedValue && String.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region INodePacketTranslatable Members

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void ITranslatable.Translate(ITranslator translator)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.WriteToStream, "write only");

            translator.Translate(ref _name);
            translator.Translate(ref _escapedValue);
            bool isImmutable = IsImmutable;
            translator.Translate(ref isImmutable);
        }

        #endregion

        /// <summary>
        /// String representation handy for tracing
        /// </summary>
        public override string ToString()
        {
            return _name + "=" + _escapedValue;
        }

        /// <summary>
        /// Called before the build when virtual properties are added, 
        /// and during the build when tasks emit properties.
        /// If name is invalid or reserved, throws ArgumentException.
        /// Creates mutable object.
        /// </summary>
        /// <remarks>
        /// Not public since the only creation scenario is setting on a project.
        /// </remarks>
        internal static ProjectPropertyInstance Create(string name, string escapedValue)
        {
            return Create(name, escapedValue, mayBeReserved: false, isImmutable: false);
        }

        /// <summary>
        /// Called before the build when virtual properties are added, 
        /// and during the build when tasks emit properties.
        /// If name is invalid or reserved, throws ArgumentException.
        /// Creates mutable object.
        /// </summary>
        /// <remarks>
        /// Not public since the only creation scenario is setting on a project.
        /// </remarks>
        internal static ProjectPropertyInstance Create(string name, string escapedValue, bool mayBeReserved)
        {
            return Create(name, escapedValue, mayBeReserved, isImmutable: false);
        }

        /// <summary>
        /// Called by the Evaluator during creation of the ProjectInstance.
        /// Reserved properties can be set with this constructor using the appropriate flag.
        /// This flags should ONLY be set by the evaluator or by cloning; after the ProjectInstance is created, they must be illegal.
        /// If name is invalid or reserved, throws ArgumentException.
        /// </summary>
        internal static ProjectPropertyInstance Create(string name, string escapedValue, bool mayBeReserved, bool isImmutable, bool isEnvironmentProperty = false, LoggingContext loggingContext = null)
        {
            return Create(name, escapedValue, mayBeReserved, null, isImmutable, isEnvironmentProperty, loggingContext);
        }

        /// <summary>
        /// Called during project build time to create a property.  Reserved properties will cause
        /// an invalid project file exception.
        /// Creates mutable object.
        /// </summary>
        internal static ProjectPropertyInstance Create(string name, string escapedValue, ElementLocation location)
        {
            return Create(name, escapedValue, false, location, isImmutable: false);
        }

        /// <summary>
        /// Called during project build time to create a property.  Reserved properties will cause
        /// an invalid project file exception.
        /// </summary>
        internal static ProjectPropertyInstance Create(string name, string escapedValue, ElementLocation location, bool isImmutable)
        {
            return Create(name, escapedValue, false, location, isImmutable);
        }

        /// <summary>
        /// Cloning constructor.
        /// Strings are immutable (copy on write) so there is no work to do
        /// </summary>
        internal static ProjectPropertyInstance Create(ProjectPropertyInstance that)
        {
            return Create(that._name, that._escapedValue, mayBeReserved: true /* already validated */, isImmutable: that.IsImmutable, that is EnvironmentDerivedProjectPropertyInstance);
        }

        /// <summary>
        /// Cloning constructor.
        /// Strings are immutable (copy on write) so there is no work to do
        /// </summary>
        internal static ProjectPropertyInstance Create(ProjectPropertyInstance that, bool isImmutable)
        {
            return Create(that._name, that._escapedValue, mayBeReserved: true /* already validated */, isImmutable: isImmutable, that is EnvironmentDerivedProjectPropertyInstance);
        }

        /// <summary>
        /// Factory for serialization
        /// </summary>
        internal static ProjectPropertyInstance FactoryForDeserialization(ITranslator translator)
        {
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.ReadFromStream, "read only");

            string name = null;
            string escapedValue = null;
            bool isImmutable = false;
            translator.Translate(ref name);
            translator.Translate(ref escapedValue);
            translator.Translate(ref isImmutable);

            return Create(name, escapedValue, mayBeReserved: true, isImmutable: isImmutable);
        }

        /// <summary>
        /// Performs a deep clone
        /// </summary>
        internal ProjectPropertyInstance DeepClone()
        {
            return Create(this);
        }

        /// <summary>
        /// Performs a deep clone, optionally changing mutability
        /// </summary>
        internal ProjectPropertyInstance DeepClone(bool isImmutable)
        {
            return Create(this, isImmutable);
        }

        /// <summary>
        /// Creates a ProjectPropertyElement representing this instance.
        /// </summary>
        /// <param name="parent">The root element to which this element will belong.</param>
        /// <returns>The new element.</returns>
        internal ProjectPropertyElement ToProjectPropertyElement(ProjectElementContainer parent)
        {
            ProjectPropertyElement property = parent.ContainingProject.CreatePropertyElement(Name);
            property.Value = EvaluatedValue;
            parent.AppendChild(property);

            return property;
        }

        /// <summary>
        /// Private constructor which throws the right sort of exception depending on whether it is invoked as a result of
        /// a design-time or build-time call.
        /// Discards the location of the original element after error checking. This is not interesting in the Execution world
        /// as it should never be needed for any subsequent messages, and is just extra bulk.
        /// Inherits mutability from project if any.
        /// </summary>
        private static ProjectPropertyInstance Create(string name, string escapedValue, bool mayBeReserved, ElementLocation location, bool isImmutable, bool isEnvironmentProperty = false, LoggingContext loggingContext = null)
        {
            // Does not check immutability as this is only called during build (which is already protected) or evaluation
            ErrorUtilities.VerifyThrowArgumentNull(escapedValue, nameof(escapedValue));
            if (location == null)
            {
                ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(name), "OM_ReservedName", name);
                ErrorUtilities.VerifyThrowArgument(mayBeReserved || !ReservedPropertyNames.IsReservedProperty(name), "OM_CannotCreateReservedProperty", name);
                XmlUtilities.VerifyThrowArgumentValidElementName(name);
            }
            else
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(!XMakeElements.ReservedItemNames.Contains(name), location, "CannotModifyReservedProperty", name);
                ProjectErrorUtilities.VerifyThrowInvalidProject(mayBeReserved || !ReservedPropertyNames.IsReservedProperty(name), location, "CannotModifyReservedProperty", name);
                XmlUtilities.VerifyThrowProjectValidElementName(name, location);
            }

            ProjectPropertyInstance instance = isEnvironmentProperty ? new EnvironmentDerivedProjectPropertyInstance(name, escapedValue, loggingContext) :
                isImmutable ? new ProjectPropertyInstanceImmutable(name, escapedValue) :
                new ProjectPropertyInstance(name, escapedValue);
            return instance;
        }

        /// <summary>
        /// Version of the class that's immutable.
        /// Could have a single class with a boolean field, but there are large numbers of these
        /// so it's important to avoid adding another field. Both types of objects are 16 bytes instead of 20.
        /// </summary>
        private class ProjectPropertyInstanceImmutable : ProjectPropertyInstance
        {
            /// <summary>
            /// Private constructor.
            /// Called by outer class factory method.
            /// </summary>
            internal ProjectPropertyInstanceImmutable(string name, string escapedValue)
                : base(name, escapedValue)
            {
            }

            /// <summary>
            /// Whether this object can be changed.
            /// An immutable object can not be made mutable.
            /// </summary>
            /// <remarks>
            /// Usually gotten from the parent ProjectInstance.
            /// </remarks>
            public override bool IsImmutable => true;
        }

        internal class EnvironmentDerivedProjectPropertyInstance : ProjectPropertyInstance
        {
            internal EnvironmentDerivedProjectPropertyInstance(string name, string escapedValue, LoggingContext loggingContext)
                : base(name, escapedValue)
            {
                this.loggingContext = loggingContext;
            }

            /// <summary>
            /// Whether this object can be changed. An immutable object cannot be made mutable.
            /// </summary>
            /// <remarks>
            /// The environment is captured at the start of the build, so environment-derived
            /// properties can't change.
            /// </remarks>
            public override bool IsImmutable => true;

            internal bool _loggedEnvProperty = false;

            internal LoggingContext loggingContext;
        }
    }
}
