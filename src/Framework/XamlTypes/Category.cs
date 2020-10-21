// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents a category to which a <see cref="BaseProperty"/> can belong to.
    /// </summary>
    /// <remarks> 
    /// Those who manually 
    /// instantiate this class should remember to call <see cref="BeginInit"/> before setting the first
    /// property and <see cref="EndInit"/> after setting the last property of the object.
    /// </remarks>
    /// <comment>
    /// This partial class contains all properties which are public and hence settable in XAML. Those properties that
    /// are internal are defined in another partial class below.
    /// </comment>
    public sealed partial class Category : CategorySchema, ISupportInitialize
    {
        #region Fields

        /// <summary>
        /// See DisplayName property.
        /// </summary>
        private string _displayName;

        #endregion

        #region Properties

        /// <summary>
        /// The name of this <see cref="Category"/>. 
        /// </summary>
        /// <remarks>
        /// This field is mandatory and culture invariant.
        /// This field cannot be set to the empty string.
        /// </remarks>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// The name that could be used by a prospective UI client to display this <see cref="Category"/>. 
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
        /// Description of this <see cref="Category"/>. 
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
        /// Subtype of this <see cref="Category"/>. Is either <c>Grid</c> (default) or <c>CommandLine</c>.
        /// </summary>
        /// <remarks>
        /// It helps the UI display this category in an appropriate form. E.g. non command line category
        /// properties are normally displayed in the form of a property grid.
        /// </remarks>
        public string Subtype
        {
            get;
            set;
        }

        /// <summary>
        /// Help information for this <see cref="Category"/>. 
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

        #endregion
    }

    /// <summary>
    /// Represents a category to which a <see cref="BaseProperty"/> can belong to.
    /// </summary>
    /// <remarks> 
    /// Those who manually 
    /// instantiate this class should remember to call <see cref="BeginInit"/> before setting the first
    /// property and <see cref="EndInit"/> after setting the last property of the object.
    /// </remarks>
    /// <comment>
    /// This partial class contains members that are auto-generated, internal, etc. Whereas the
    /// other partial class contains public properties that can be set in XAML.
    /// </comment>
    public sealed partial class Category : CategorySchema, ISupportInitialize
    {
        // This partial class contains members that are auto-generated, internal, etc.
        #region Constructor

        /// <summary>
        /// Default constructor. Called during deserialization.
        /// </summary>
        public Category()
        {
            Subtype = "Grid";
        }

        #endregion // Constructor

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
