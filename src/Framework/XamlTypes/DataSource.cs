// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Indicates where the default value for some property may be found.
    /// </summary>
    public enum DefaultValueSourceLocation
    {
        /// <summary>
        /// The default value for a property is set at the top of the project file (usually via an import of a .props file).
        /// </summary>
        BeforeContext,

        /// <summary>
        /// The default value for a property is set at the bottom of the project file (usually via an import of a .targets file,
        /// where the property definition is conditional on whether the property has not already been defined.)
        /// </summary>
        AfterContext,
    }

    /// <summary>
    /// Represents the location and grouping for a <see cref="BaseProperty"/>.
    /// </summary>
    /// <remarks>
    /// Those who manually
    /// instantiate this class should remember to call <see cref="BeginInit"/> before setting the first
    /// property and <see cref="EndInit"/> after setting the last property of the object.
    /// </remarks>
    public sealed class DataSource : ISupportInitialize
    {
        #region Constructor

        /// <summary>
        /// Default constructor. Needed for proper XAML deserialization.
        /// </summary>
        public DataSource()
        {
            // Set the default value for this property.
            HasConfigurationCondition = true;
            Label = String.Empty;
            SourceOfDefaultValue = DefaultValueSourceLocation.BeforeContext;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The storage location for this data source.
        /// </summary>
        /// <remarks>
        /// This field is mandatory unless <see cref="PersistenceStyle"/> is set. In that case, the parent
        /// <see cref="DataSource"/> will be used with the specified style. Example values are <c>ProjectFile</c> and
        /// <c>UserFile</c>. <c>ProjectFile</c> causes the property value to be written to and read from the project
        /// manifest file or the property sheet (depending on which node in the solution explorer/property manager window
        /// is used to spawn the property pages UI). <c>UserFile</c> causes the property value to be written to and read
        /// from the .user file.
        /// </remarks>
        public string Persistence
        {
            get;
            set;
        }

        /// <summary>
        /// The storage style for this data source.
        /// </summary>
        /// <remarks>
        /// For example, with <see cref="Persistence"/> of <c>ProjectFile</c>, this field can be <c>Element</c> (default) to
        /// save as a child XML Element, or <c>Attribute</c> to save properties as an XML attribute.
        /// </remarks>
        public string PersistenceStyle
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the actual MSBuild property name used to read/write the value of this property.
        /// Applicable only to <see cref="DataSource"/> objects attached to properties.
        /// </summary>
        /// <value>The MSBuild property name to use; or <c>null</c> to use the <see cref="BaseProperty.Name"/> as the MSBuild property name.</value>
        /// <remarks>
        /// <para>The persisted name will usually be the same as the property name as it appears in the <see cref="Rule"/>
        /// and the value of this property can therefore be left at is default of <c>null</c>.
        /// Since property names must be unique but need not be unique in the persisted store (due to other differences
        /// in the data source such as item type) there may be times when Rule property names must be changed to be
        /// unique in the XAML file, but without changing how the property is persisted in the MSBuild file.
        /// It is in those cases where this property becomes useful.</para>
        /// <para>It may also be useful in specialized build environments where property names must differ from the
        /// normally used name in order to maintain compatibility with the project system.</para>
        /// </remarks>
        public string PersistedName
        {
            get;
            set;
        }

        /// <summary>
        /// The label of the MSBuild property group/item definition group to which
        /// a property/item definition metadata belongs to. Default value is the
        /// empty string.
        /// </summary>
        /// <example> A VC++ property that exists in the project manifest
        /// in the MSBuild property group with label <c>Globals</c> would have this
        /// same value for this field. </example>
        public string Label
        {
            get;
            set;
        }

        /// <summary>
        /// If a <see cref="BaseProperty"/> is an item definition metadata or item metadata, this field
        /// specified the item type of the item definition or the item, respectively. For common properties
        /// this field must not be set.
        /// </summary>
        public string ItemType
        {
            get;
            set;
        }

        /// <summary>
        /// Indicates if a property is configuration-dependent as indicated by the presence of a configuration
        /// condition attached to the property definition at its persistence location.
        /// </summary>
        /// <remarks>
        /// This field is optional and has the default value of <c>true</c>.
        /// </remarks>
        public bool HasConfigurationCondition
        {
            get;
            set;
        }

        /// <summary>
        /// The data type of the source.  Generally one of <c>Item</c>, <c>ItemDefinition</c>, <c>Property</c>,
        /// or <c>TargetResults</c> (when <see cref="MSBuildTarget"/> is non-empty).
        /// Among other things this governs how the data is treated during build.
        /// </summary>
        /// <example>
        /// A value of <c>Item</c> for this property indicates that this property is actually
        /// an item array - the list of all items with the item type specified by <see cref="ItemType"/>.
        /// </example>
        public string SourceType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the semicolon-delimited list of MSBuild targets that must be executed before reading
        /// the read-only properties or items described by this <see cref="DataSource"/>.
        /// </summary>
        public string MSBuildTarget
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating where the default value for this property can be found.
        /// </summary>
        public DefaultValueSourceLocation SourceOfDefaultValue
        {
            get;
            set;
        }

        #endregion // Properties

        #region ISupportInitialize Members

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public void BeginInit()
        {
        }

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public void EndInit()
        {
        }

        #endregion
    }
}
