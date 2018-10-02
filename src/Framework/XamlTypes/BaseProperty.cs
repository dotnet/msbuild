// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Markup;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents a <see cref="Rule"/> property. 
    /// </summary>
    /// <remarks> 
    /// <para>This represents schema information (name, allowed values, etc) of a <see cref="Rule"/> property.
    /// Since this is just schema information, there is no field like "Value" used to get/set the value of this
    /// property.</para>
    /// <para> Those who manually instantiate this class should remember to call <see cref="BeginInit"/> before
    /// setting the first property and <see cref="EndInit"/> after setting the last property of the object.</para>
    /// </remarks>
    /// <comment>
    /// This partial class contains all properties which are public and hence settable in XAML. Those properties that
    /// are internal are defined in another partial class below.
    /// </comment>
    [ContentProperty("Arguments")]
    public abstract partial class BaseProperty : ISupportInitialize
    {
        #region Fields

        /// <summary>
        /// See DisplayName property.
        /// </summary>
        private string _displayName;

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor. Needed for deserializtion from a persisted format.
        /// </summary>
        protected BaseProperty()
        {
            // Initialize collection properties in this class. This is required for
            // proper deserialization.
            Metadata = new List<NameValuePair>();
            Arguments = new List<Argument>();
            ValueEditors = new List<ValueEditor>();

            // The default value of Visible.
            Visible = true;

            // The default value of IncludeInCommandLine.
            IncludeInCommandLine = true;

            SwitchPrefix = String.Empty;
            Separator = String.Empty;
            Category = "General";
            Subcategory = String.Empty;

            HelpContext = -1;
            HelpFile = String.Empty;
            HelpUrl = String.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The name of this <see cref="BaseProperty"/>. 
        /// </summary>
        /// <remarks>
        /// This field is mandatory and culture invariant. The value of this field cannot be set to the empty string. 
        /// </remarks>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// The name that could be used by a prospective UI client to display this <see cref="BaseProperty"/>. 
        /// </summary>
        /// <remarks>
        /// This field is optional and is culture sensitive. When this property is not set, it is assigned the same 
        /// value as the <see cref="Name"/> property (and hence, would not be localized).
        /// </remarks>
        [Localizable(true)]
        public string DisplayName
        {
            get
            {
                return _displayName ?? Name;
            }

            set
            {
                _displayName = value;
            }
        }

        /// <summary>
        /// Description of this <see cref="BaseProperty"/> for use by a prospective UI client. 
        /// </summary>
        /// <remarks> 
        /// This field is optional and is culture sensitive.
        /// </remarks>
        [Localizable(true)]
        public string Description
        {
            get;
            set;
        }

        /// <summary>
        /// The keyword that is used to open the help page for this property.
        /// </summary>
        /// <remarks>
        /// This form of specifying help takes precedence over <see cref="HelpUrl"/>
        /// and <see cref="HelpFile"/> + <see cref="HelpContext"/>.
        /// This field is optional and is culture insensitive.
        /// </remarks>
        [Localizable(false)]
        public string F1Keyword
        {
            get;
            set;
        }

        /// <summary>
        /// The URL of the help page for this property that will be opened when the user hits F1.
        /// </summary>
        /// <remarks>
        /// This property is higher in priority that <see cref="HelpContext"/> + <see cref="HelpFile"/> 
        /// (i.e., these two properties are ignored if <see cref="HelpUrl"/>
        /// is specified), but lower in priority than <see cref="F1Keyword"/>.
        /// This field is optional and is culture insensitive.
        /// </remarks>
        /// <example> <c>ms-help://MS.VSCC.v80/MS.MSDN.v80/MS.VisualStudio.v80.en/dv_vstoc/html/06ddebea-2c83-4a45-bb48-6264c797ed93.htm</c> </example>
        [Localizable(false)]
        [SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public string HelpUrl
        {
            get;
            set;
        }

        /// <summary>
        /// The help file to use when the user hits F1. Must specify <see cref="HelpContext"/> along with this.
        /// </summary>
        /// <remarks> 
        /// This property goes along with <see cref="HelpContext"/>. <seealso cref="HelpContext"/>. This
        /// form of specifying the help page for a property takes lower precedence than both <see cref="F1Keyword"/>
        /// and <see cref="HelpUrl"/>.
        /// This field is optional and is culture insensitive.
        /// </remarks>
        [Localizable(false)]
        public string HelpFile
        {
            get;
            set;
        }

        /// <summary>
        /// The help context to use when the user hits F1. Must specify <see cref="HelpFile"/> along with this.
        /// </summary>
        /// <remarks>
        /// This property uses the <see cref="HelpFile"/> property to display the help context of the specified 
        /// help file. This field is optional. This
        /// form of specifying the help page for a property takes lower precedence than both <see cref="F1Keyword"/>
        /// and <see cref="HelpUrl"/>.
        /// </remarks>
        public int HelpContext
        {
            get;
            set;
        }

        /// <summary>
        /// The name of the category to which this property belongs to. 
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the value of this field  does not correspond to the <c>Name</c> 
        /// property of a <see cref="Category"/> element defined in
        /// the containing <see cref="Rule"/>, a default <see cref="Category"/> with this name
        /// is auto-generated and added to the containing <see cref="Rule"/> class. 
        /// </para>
        /// <para>
        /// This field is optional and is culture invariant. 
        /// </para>
        /// <para>
        /// When this field is not specified, this property is added to a
        /// auto-generated category called <c>General</c> (localized). This field cannot be set to the
        /// empty string.
        /// </para>
        /// </remarks>
        public string Category
        {
            get;
            set;
        }

        /// <summary>
        /// The sub category to which this property belongs to.
        /// </summary>
        public string Subcategory
        {
            get;
            set;
        }

        /// <summary>
        /// Tells if this property is a read-only property. 
        /// </summary>
        /// <remarks>
        /// This field is optional and its default value is "false".
        /// </remarks>
        public bool ReadOnly
        {
            get;
            set;
        }

        /// <summary>
        /// A value indicating whether this property allows multiple values to be supplied/selected simultaneously.
        /// </summary>
        public bool MultipleValuesAllowed
        {
            get;
            set;
        }

        /// <summary>
        /// The switch representation of this property for the case when this property represents a tool parameter.
        /// </summary>
        /// <remarks>
        /// This field is optional and culture invariant.
        /// </remarks>
        /// <example>
        /// For the VC++ CL task, <c>WholeProgramOptimization</c> is a boolean parameter. It's switch is <c>GL</c>.
        /// </example>
        public string Switch
        {
            get;
            set;
        }

        /// <summary>
        /// The prefix for the switch representation of this property for the case when this property represents a tool parameter.
        /// </summary>
        /// <remarks>
        /// The value specified here overrides the value specified for the parent <see cref="Rule"/>'s <see cref="Rule.SwitchPrefix"/>.
        /// This field is optional and culture invariant.
        /// </remarks>
        /// <example>
        /// For the VC++ CL task, <c>WholeProgramOptimization</c> is a boolean parameter. It's switch is <c>GL</c> and its
        /// switch prefix (inherited from the parent <see cref="Rule.SwitchPrefix"/> since it is not overridden by <c>WholeProgramOptimization</c>)
        /// is <c>/</c>. Thus the complete switch in the command line for this property would be <c>/GL</c>
        /// </example>
        public string SwitchPrefix
        {
            get;
            set;
        }

        /// <summary>
        /// The token used to separate a switch from its value.
        /// </summary>
        /// <remarks>
        /// The value specified here overrides the value specified for the parent <see cref="Rule"/>'s <see cref="Rule.Separator"/>.
        /// This field is optional and culture invariant.
        /// </remarks>
        /// <example>
        /// Example: Consider <c>/D:WIN32</c>. In this switch and value representation, ":" is the separator since its separates the switch <c>D</c> 
        /// from its value <c>WIN32</c>.
        /// </example>
        public string Separator
        {
            get;
            set;
        }

        /// <summary>
        /// A hint to the UI client telling it whether to display this property or not.
        /// </summary>
        /// <remarks>
        /// This field is optional and has the default value of "true".
        /// </remarks>
        public bool Visible
        {
            get;
            set;
        }

        /// <summary>
        /// A hint to the command line constructor whether to include this property in the command line or not.
        /// </summary>
        /// <remarks>
        /// Some properties are used only by the targets and don't want to be included in the command line.
        /// Others (like task parameters) are included in the command line in the form of the switch/value they emit.
        /// This field is optional and has the default value of <c>true</c>.
        /// </remarks>
        public bool IncludeInCommandLine
        {
            get;
            set;
        }

        /// <summary>
        /// Indicates whether this property is required to have a value set.
        /// </summary>
        public bool IsRequired
        {
            get;
            set;
        }

        /// <summary>
        /// Specifies the default value for this property. 
        /// </summary>
        /// <remarks>
        /// This field is optional and whether, for a <see cref="StringProperty"/>,
        /// it is culture sensitive or not depends on the semantics of it.
        /// </remarks>
        [Localizable(true)]
        public string Default
        {
            get;
            set;
        }

        /// <summary>
        /// The data source where the current value of this property is stored. 
        /// </summary>
        /// <remarks>
        /// If defined, it overrides the 
        /// <see cref="Rule.DataSource"/> property on the containing <see cref="Rule"/>. This field is mandatory only if the parent
        /// <see cref="Rule"/> does not have the data source initialized. The getter for this property returns
        /// only the <see cref="DataSource"/> set directly on this <see cref="BaseProperty"/> instance.
        /// </remarks>
        public DataSource DataSource
        {
            get;
            set;
        }

        /// <summary>
        /// Additional attributes of this <see cref="BaseProperty"/>. 
        /// </summary>
        /// <remarks>
        /// This can be used as a grab bag of additional metadata of this property that are not
        /// captured by the primary fields. You will need a custom UI to interpret the additional
        /// metadata since the shipped UI formats can't obviously know about it.
        /// This field is optional.
        /// </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<NameValuePair> Metadata
        {
            get;
            set;
        }

        /// <summary>
        /// List of arguments for this property.
        /// </summary>
        /// <remarks>
        ///  This field is optional.
        /// </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<Argument> Arguments
        {
            get;
            set;
        }

        /// <summary>
        /// List of value editors for this property. 
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<ValueEditor> ValueEditors
        {
            get;
            set;
        }

        #endregion
    }

    /// <summary>
    /// Represents a <see cref="Rule"/> property. 
    /// </summary>
    /// <remarks> 
    /// <para>This represents schema information (name, allowed values, etc) of a <see cref="Rule"/> property.
    /// Since this is just schema information, there is no field like "Value" used to get/set the value of this
    /// property.</para>
    /// <para> Those who manually instantiate this class should remember to call <see cref="BeginInit"/> before
    /// setting the first property and <see cref="EndInit"/> after setting the last property of the object.</para>
    /// </remarks>
    /// <comment>
    /// This partial class contains members that are auto-generated, internal, etc. Whereas the
    /// other partial class contains public properties that can be set in XAML.
    /// </comment>
    public abstract partial class BaseProperty : ISupportInitialize
    {
        #region Properties

        /// <summary>
        /// The <see cref="Rule"/> containing this <see cref="BaseProperty"/>.
        /// </summary>
        public Rule ContainingRule
        {
            get;
            internal set;
        }

        #endregion // Properties

        #region ISupportInitialize Members

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public virtual void BeginInit()
        {
        }

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public virtual void EndInit()
        {
        }

        #endregion
    }
}
