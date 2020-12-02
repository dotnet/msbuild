// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Markup;

namespace Microsoft.Build.Framework.XamlTypes
{
    // DEV11DESIGNISSUE (pavana): Once the de-serialization is complete, seal the type so
    // that the object becomes immutable (e.g. we can set a flag in EndInit() and throw
    // in all property setter if this flag is set.

    /// <summary>
    /// Methods for overriding one rule with another.
    /// </summary>
    public enum RuleOverrideMode
    {
        /// <summary>
        /// A subsequent definition for a rule (with the same name) entirely overrides a previous definition.
        /// </summary>
        Replace,

        /// <summary>
        /// A subsequent definition for a rule (with the same name) adds properties to a previous definition.
        /// </summary>
        Extend,
    }

    /// <summary>
    /// Used to represent the schema information for a Tool, a Custom Build Rule, a PropertyPage, etc. 
    /// </summary> 
    /// <remarks> 
    /// <para>
    /// Normally represented on disk as XAML, only one instance of this class is maintained per XAML
    /// file per project engine (solution).
    /// </para>
    /// <para> Those who manually instantiate this class should remember to call <see cref="BeginInit"/> before
    /// setting the first property and <see cref="EndInit"/> after setting the last property of the object.
    /// </para>
    /// </remarks>
    /// <comment>
    /// This partial class contains all properties which are public and hence settable in XAML. Those properties that
    /// are internal are defined in another partial class below.
    /// </comment>
    [ContentProperty("Properties")]
    [DebuggerDisplay("Rule: {Name}")]
    public sealed partial class Rule : RuleSchema, ISupportInitialize, IProjectSchemaNode
    {
        #region Fields

        /// <summary>
        /// See DisplayName property.
        /// </summary>
        private string _displayName;

        #endregion // Fields

        #region Constructor

        /// <summary>
        /// Default constructor. Needed for deserialization from a persisted format.
        /// </summary>
        public Rule()
        {
            // Initialize collection properties in this class. This is required for
            // proper deserialization.
            Properties = new List<BaseProperty>();
            Categories = new List<Category>();
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Set defaults.
            SupportsFileBatching = false;
            ShowOnlyRuleProperties = true;
            SwitchPrefix = String.Empty;
            Separator = String.Empty;
            OverrideMode = RuleOverrideMode.Replace;
            PropertyPagesHidden = false;
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// The name of this <see cref="Rule"/>. 
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
        /// The name of the tool executable when this rule represents a tool.
        /// </summary>
        public string ToolName
        {
            get;
            set;
        }

        /// <summary>
        /// Description of this <see cref="Rule"/> for use by a prospective UI client. 
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
        /// Help information for this <see cref="Rule"/>. 
        /// </summary>
        /// <remarks>
        /// Maybe used to specify a help URL. This field
        /// is optional and is culture sensitive.
        /// </remarks>
        [Localizable(true)]
        public string HelpString
        {
            get;
            set;
        }

        /// <summary>
        /// The prefix to use for all property switches in this <see cref="Rule"/> for the case when this property <see cref="Rule"/> represent a tool.
        /// </summary>
        /// <remarks>
        /// The value specified can be overridden by the value specified by a child <see cref="BaseProperty"/>'s <see cref="BaseProperty.SwitchPrefix"/>.
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
        /// The token used to separate a property switch from its value.
        /// </summary>
        /// <remarks>
        /// The value specified here is overridden by the value specified by the child <see cref="BaseProperty"/>'s <see cref="BaseProperty.Separator"/>.
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
        /// The UI renderer template used to display this Rule. 
        /// </summary>
        /// <remarks>
        /// The value used to set
        /// this field can be anything as long as it is recognized by the intended renderer.
        /// This field is required only if this Rule is meant to be displayed as a property page.
        /// </remarks>
        public string PageTemplate
        {
            get;
            set;
        }

        /// <summary>
        /// The <see cref="DataSource"/> for all the properties in this <see cref="Rule"/>. This is overriden by any
        /// data source defined locally for a property. 
        /// </summary>
        /// <remarks>
        /// This field need not be specified only if all individual properties have data source defined locally.
        /// </remarks>
        public DataSource DataSource
        {
            get;
            set;
        }

        /// <summary>
        /// This is a suggestion to a prospective UI client on the relative location of this <see cref="Rule"/> compared to all other Rules in the system.
        /// </summary>
        public int Order
        {
            get;
            set;
        }

        /// <summary>
        /// This is used to specify whether multiple files need to be batched on one command line invocation. 
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public bool SupportsFileBatching
        {
            get;
            set;
        }

        /// <summary>
        /// Indicates whether to hide the command line category or not. Default value is true.
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public bool ShowOnlyRuleProperties
        {
            get;
            set;
        }

        /// <summary>
        /// When this <see cref="Rule"/> represents a Build Customization, this field represents the file extension to associate.
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public string FileExtension
        {
            get;
            set;
        }

        /// <summary>
        /// When this <see cref="Rule"/> represents a Build Customization, this field represents the message to be displayed before executing a Build Customization during the build.
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public string ExecutionDescription
        {
            get;
            set;
        }

        /// <summary>
        /// When this <see cref="Rule"/> represents a Build Customization, this field represents the command line template that is going to be used by a Build Customization task to invoke the tool.
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public string CommandLine
        {
            get;
            set;
        }

        /// <summary>
        /// When this <see cref="Rule"/> represents a Build Customization, this field defines the semicolon separated list of additional inputs that are going to be evaluated
        /// for the Build Customization target.
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public string AdditionalInputs
        {
            get;
            set;
        }

        /// <summary>
        /// When this <see cref="Rule"/> represents a Build Customization, this field defines the semicolon separated list of outputs that are going to be evaluated
        /// for the Build Customization target.
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        public string Outputs
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the method to use when multiple rules with the same name appear in the project
        /// to reconcile the rules into one instance.
        /// </summary>
        public RuleOverrideMode OverrideMode
        {
            get;
            set;
        }

        /// <summary>
        /// This list of properties in this <see cref="Rule"/>. Atleast one property should be specified.
        /// </summary>
        /// <remarks> The list returned by this property should not be modified. </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<BaseProperty> Properties
        {
            get;
            set;
        }

        /// <summary>
        /// The list of <see cref="Category"/>s that properties in this <see cref="Rule"/> belong to. 
        /// </summary>
        /// <remarks>
        /// This field is optional. Note that this field returns only the categories that were explicitly defined and do
        /// not contain any auto-generated categories. When a <see cref="BaseProperty"/> contained in this <see cref="Rule"/>
        /// declares its category to be something that is not present in this list, then we auto-generate a <see cref="Category"/>
        /// with that name and add it to the internal list of categories. That auto-generated category will not be returned
        /// by this field.
        /// </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<Category> Categories
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets arbitrary metadata that may be set on a rule.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Shipped this way in Dev11 Beta, which is go-live")]
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if property pages for this rule should be hidden or not.
        /// </summary>
        public bool PropertyPagesHidden { get; set; }

        #endregion
    }

    /// <summary>
    /// Used to represent the schema information for a Tool, a Custom Build Rule, a PropertyPage, etc. 
    /// </summary> 
    /// <remarks> 
    /// <para>
    /// Normally represented on disk as XAML, only one instance of this class is maintained per XAML
    /// file per project engine (solution).
    /// </para>
    /// <para> Those who manually instantiate this class should remember to call <see cref="BeginInit"/> before
    /// setting the first property and <see cref="EndInit"/> after setting the last property of the object.
    /// </para>
    /// </remarks>
    /// <comment>
    /// This partial class contains members that are auto-generated, internal, etc. Whereas the
    /// other partial class contains public properties that can be set in XAML.
    /// </comment>
    public sealed partial class Rule : RuleSchema, ISupportInitialize, IProjectSchemaNode
    {
        #region Fields

        /// <summary>
        /// Thread synchronization.
        /// </summary>
        private object _syncObject = new object();

        /// <summary>
        /// See the <see cref="EvaluatedCategories"/> property.
        /// </summary>
        private List<Category> _evaluatedCategories;

        /// <summary>
        /// Ordered dictionary of category names and the properties contained in them.
        /// The order of the categories is exactly the same as that specified in the XAML file.
        /// </summary>
        private OrderedDictionary _categoryNamePropertyListMap;

        /// <summary>
        /// A lookup cache of property names to properties.
        /// </summary>
        private ReadOnlyDictionary<string, BaseProperty> _propertiesByNameMap;

        #endregion

        #region Properties

        /// <summary>
        /// This property returns the union of XAML specified <see cref="Category"/>s and auto-generated 
        /// <see cref="Category"/>s. The latter are created from the missing categories that are being referred to by the 
        /// properties in this Rule. The auto-generated <see cref="Category"/>s only have their name set.
        /// </summary>
        public List<Category> EvaluatedCategories
        {
            get
            {
                // check-lock-check pattern DOESN'T work here because two fields get initialized within this lazy initialization method.
                lock (_syncObject)
                {
                    if (_evaluatedCategories == null)
                    {
                        CreateCategoryNamePropertyListMap();
                    }

                    return _evaluatedCategories;
                }
            }
        }

        #endregion // Properties

        #region Public Methods

        /// <summary>
        /// Returns all properties partitioned into categories. The return value is never
        /// null. 
        /// The returned list may contain auto-generated categories. Note that if a <see cref="BaseProperty"/>
        /// (or its derived classes) refer to a property that is not specified, then an new
        /// Category is generated for the same. If not category is specified for the property, then
        /// the property is placed in the "General" category.
        /// The list of categories is exactly as specified in the Xaml file. The auto-generated
        /// categories come (in no strict order) after the specified categories.
        /// </summary>
        /// <returns> A dictionary whose keys are the <see cref="Category"/> names and 
        /// the value is the list of properties in that category. </returns>
        public OrderedDictionary GetPropertiesByCategory()
        {
            // check-lock-check pattern DOESN'T work here because two fields get initialized within this lazy initialization method.
            lock (_syncObject)
            {
                if (_categoryNamePropertyListMap == null)
                {
                    CreateCategoryNamePropertyListMap();
                }

                return _categoryNamePropertyListMap;
            }
        }

        /// <summary>
        /// Returns the list of properties in a <see cref="Category"/>. Returns null if this <see cref="Rule"/>
        /// doesn't contain this category.
        /// </summary>
        public IList<BaseProperty> GetPropertiesInCategory(string categoryName)
        {
            // check-lock-check pattern DOESN'T work here because two fields get initialized within this lazy initialization method.
            lock (_syncObject)
            {
                if (_categoryNamePropertyListMap == null)
                {
                    CreateCategoryNamePropertyListMap();
                }

                return _categoryNamePropertyListMap[categoryName] as IList<BaseProperty>;
            }
        }

        /// <summary>
        /// Returns a property with a given name.
        /// </summary>
        /// <returns>The property, or <c>null</c> if one with a matching name could not be found.</returns>
        public BaseProperty GetProperty(string propertyName)
        {
            if (_propertiesByNameMap == null)
            {
                lock (_syncObject)
                {
                    if (_propertiesByNameMap == null)
                    {
                        var map = new Dictionary<string, BaseProperty>(this.Properties.Count, StringComparer.OrdinalIgnoreCase);
                        foreach (var property in this.Properties)
                        {
                            map[property.Name] = property;
                        }

                        _propertiesByNameMap = new ReadOnlyDictionary<string, BaseProperty>(map);
                    }
                }
            }

            BaseProperty result;
            _propertiesByNameMap.TryGetValue(propertyName, out result);
            return result;
        }

        #endregion // Public Methods

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
            Initialize();
        }

        #endregion

        #region IProjectSchemaNode Members
        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<Type> GetSchemaObjectTypes()
        {
            yield return typeof(Rule);
        }

        /// <summary>
        /// see IProjectSchemaNode
        /// </summary>
        public IEnumerable<object> GetSchemaObjects(Type type)
        {
            if (type == typeof(Rule))
            {
                yield return this;
            }
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes this class after Xaml loading is done.
        /// </summary>
        private void Initialize()
        {
            if (Properties != null)
            {
                // Set parent pointers on all containing properties.
                foreach (BaseProperty property in Properties)
                {
                    property.ContainingRule = this;
                }
            }
        }

        /// <summary>
        /// Creates a map containing all the evaluated category names and the list of
        /// properties belonging to that category.
        /// </summary>
        private void CreateCategoryNamePropertyListMap()
        {
            lock (_syncObject)
            {
                _evaluatedCategories = new List<Category>();

                if (Categories != null)
                {
                    _evaluatedCategories.AddRange(Categories);
                }

                _categoryNamePropertyListMap = new OrderedDictionary();

                foreach (Category category in Categories)
                {
                    _categoryNamePropertyListMap.Add(category.Name, new List<BaseProperty>());
                }

                foreach (BaseProperty property in Properties)
                {
                    // If a property refers to a category which does not have an entry in the Xaml file,
                    // create a category object ourselves.
                    if (!_categoryNamePropertyListMap.Contains(property.Category))
                    {
                        Category category = new Category();
                        category.Name = property.Category;

                        _evaluatedCategories.Add(category);
                        _categoryNamePropertyListMap.Add(category.Name, new List<BaseProperty>());
                    }

                    List<BaseProperty> propertiesInTheSameCategory = _categoryNamePropertyListMap[property.Category] as List<BaseProperty>;
                    propertiesInTheSameCategory.Add(property);
                }
            }
        }

        #endregion
    }
}
