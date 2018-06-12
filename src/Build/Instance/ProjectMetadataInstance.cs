// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Wraps an evaluated piece of metadata for build purposes.</summary>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Construction;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an evaluated piece of metadata for build purposes
    /// Added and removed via methods on the ProjectItemInstance object.
    /// IMMUTABLE OBJECT.
    /// </summary>
    [DebuggerDisplay("{_name}={EvaluatedValue}")]
    public class ProjectMetadataInstance : IKeyed, IValued, IEquatable<ProjectMetadataInstance>, INodePacketTranslatable, IMetadatum, IDeepCloneable<ProjectMetadataInstance>, IImmutable
    {
        /// <summary>
        /// Name of the metadatum
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// Evaluated value
        /// Never null.
        /// </summary>
        private readonly string _escapedValue;

        /// <summary>
        /// Constructor for metadata.
        /// Does not allow item spec modifiers.
        /// Discards the location of the original element. This is not interesting in the Execution world
        /// as it should never be needed for any subsequent messages, and is just extra bulk.
        /// IMMUTABLE OBJECT.
        /// </summary>
        /// <remarks>
        /// Not public since the only creation scenario is setting on an item
        /// </remarks>
        internal ProjectMetadataInstance(string name, string escapedValue)
            : this(name, escapedValue, false)
        {
        }

        /// <summary>
        /// Constructor for metadata.
        /// Called when a ProjectInstance is created, before the build
        /// when virtual items are added, and during the build when tasks
        /// emit items.
        /// Discards the location of the original element. This is not interesting in the Execution world
        /// as it should never be needed for any subsequent messages, and is just extra bulk.
        /// IMMUTABLE OBJECT.
        /// If the value passed in is null, will be changed to String.Empty.
        /// </summary>
        /// <remarks>
        /// Not public since the only creation scenario is setting on an item
        /// </remarks>
        internal ProjectMetadataInstance(string name, string escapedValue, bool allowItemSpecModifiers)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");

            if (allowItemSpecModifiers)
            {
                ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(name), "OM_ReservedName", name);
            }
            else
            {
                ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(name) && !FileUtilities.ItemSpecModifiers.IsItemSpecModifier(name), "OM_ReservedName", name);
            }

            _name = name;
            _escapedValue = escapedValue ?? String.Empty;
        }

        /// <summary>
        /// Constructor for metadata from a ProjectMetadata.
        /// Called when a ProjectInstance is created.
        /// IMMUTABLE OBJECT.
        /// </summary>
        internal ProjectMetadataInstance(ProjectMetadata metadatum)
            : this(metadatum.Name, metadatum.EvaluatedValueEscaped, false)
        {
        }

        /// <summary>
        /// Private constructor used for serialization
        /// </summary>
        private ProjectMetadataInstance(INodePacketTranslator translator)
        {
            translator.Translate(ref _name);
            translator.Translate(ref _escapedValue);
        }

        /// <summary>
        /// Name of the metadata
        /// </summary>
        /// <remarks>
        /// This cannot be set, as it is used as the key into 
        /// the item's metadata table.
        /// </remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Name
        {
            [DebuggerStepThrough]
            get
            { return _name; }
        }

        /// <summary>
        /// Evaluated value of the metadatum. 
        /// Never null.
        /// </summary>
        public string EvaluatedValue
        {
            [DebuggerStepThrough]
            get
            {
                return EscapingUtilities.UnescapeAll(_escapedValue);
            }
        }

        /// <summary>
        /// Implementation of IKeyed exposing the metadatum name
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
            { return EvaluatedValueEscaped; }
        }

        /// <summary>
        /// Evaluated and escaped value of the metadata.
        /// Never null.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string EvaluatedValueEscaped
        {
            [DebuggerStepThrough]
            get
            {
                return _escapedValue;
            }
        }

        /// <summary>
        /// String representation handy for tracing
        /// </summary>
        public override string ToString()
        {
            return _name + "=" + _escapedValue;
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            // Read implementation is directly in the constructor so that fields can be read-only
            ErrorUtilities.VerifyThrow(translator.Mode == TranslationDirection.WriteToStream, "write only");

            string mutableName = _name;
            string mutableValue = _escapedValue;
            translator.Translate(ref mutableName);
            translator.Translate(ref mutableValue);
        }

        #endregion

        #region IEquatable<ProjectMetadataInstance> Members

        /// <summary>
        /// Compares this metadata to another for equivalence.
        /// </summary>
        /// <param name="other">The other metadata</param>
        /// <returns>True if they are equivalent, false otherwise.</returns>
        bool IEquatable<ProjectMetadataInstance>.Equals(ProjectMetadataInstance other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return (_escapedValue == other._escapedValue &&
                    String.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        /// <summary>
        /// Deep clone the metadata
        /// Strings are immutable (copy on write) so there is no work to do.
        /// Allows built-in metadata names, as they are still valid on the new metadatum.
        /// </summary>
        /// <returns>A new metadata instance.</returns>
        public ProjectMetadataInstance DeepClone()
        {
            return new ProjectMetadataInstance(_name, _escapedValue, true /* allow built-in metadata names */);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static ProjectMetadataInstance FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new ProjectMetadataInstance(translator);
        }
    }
}
