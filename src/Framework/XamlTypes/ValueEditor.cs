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
    /// Represents a value editor 
    /// </summary>
    [ContentProperty("Metadata")]
    public sealed class ValueEditor : ISupportInitialize
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
        public ValueEditor()
        {
            Metadata = new List<NameValuePair>();
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// The name of this <see cref="ValueEditor"/>. This field is mandatory and culture invariant.
        /// </summary>
        public string EditorType
        {
            get;
            set;
        }

        /// <summary>
        /// The UI display name for the editor
        /// </summary>
        [Localizable(true)]
        public string DisplayName
        {
            get
            {
                return _displayName ?? String.Empty;
            }

            set
            {
                _displayName = value;
            }
        }

        /// <summary>
        /// Additional attributes of the editor that are not generic enough to be made
        /// properties on this class. This field is optional.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<NameValuePair> Metadata
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
