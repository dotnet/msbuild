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
    /// Represents an admissible value of an <see cref="EnumProperty"/>.
    /// </summary>
    [ContentProperty("Arguments")]
    public sealed class EnumValue
    {
        #region Fields

        /// <summary>
        /// See DisplayName property.
        /// </summary>
        private string _displayName;

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor needed for XAML deserialization.
        /// </summary>
        public EnumValue()
        {
            Arguments = new List<Argument>();
            Metadata = new List<NameValuePair>();

            SwitchPrefix = String.Empty;
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// The name of this <see cref="EnumValue"/>. 
        /// </summary>
        /// <remarks>
        /// This field is mandatory and culture invariant.
        /// </remarks>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// The name that could be used by a prospective UI client to display this <see cref="EnumValue"/>. 
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
        /// Help information for this <see cref="EnumValue"/>. 
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
        /// The switch representation of this property for the case when the parent <see cref="EnumProperty"/> represents a tool parameter.
        /// </summary>
        /// <remarks>
        /// This field is optional and culture invariant.
        /// </remarks>
        /// <example> The VC compiler has an <see cref="EnumProperty"/> named <c>Optimization</c>used to specify the desired optimization type. All the
        /// admissible values for this property have switches, e.g. <c>Disabled</c> (switch = <c>Od</c>), "MinimumSize" (switch = <c>O1</c>), 
        /// etc. </example>
        public string Switch
        {
            get;
            set;
        }

        /// <summary>
        /// The prefix for the switch representation of this value for the case when the parent <see cref="EnumProperty"/> represents a tool parameter.
        /// </summary>
        /// <remarks>
        /// This field is optional and culture invariant.
        /// </remarks>
        public string SwitchPrefix
        {
            get;
            set;
        }

        /// <summary>
        /// Tells if this <see cref="EnumValue"/> is the default value for the associated
        /// <see cref="EnumProperty"/>. 
        /// </summary>
        /// <remarks>
        /// This field is optional and the default value for this
        /// field is "false".
        /// </remarks>
        public bool IsDefault
        {
            get;
            set;
        }

        /// <summary>
        /// Additional attributes of this <see cref="EnumValue"/>. 
        /// </summary>
        /// <remarks>
        /// This can be used as a grab bag of additional metadata of this value that are not
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
        /// List of arguments for this <see cref="EnumValue"/>. 
        /// </summary>
        /// <remarks>
        /// This field is optional.
        /// </remarks>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<Argument> Arguments
        {
            get;
            set;
        }

        #endregion 
    }
}
