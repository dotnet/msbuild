// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Windows.Markup;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents an argument to a <see cref="BaseProperty"/>. 
    /// </summary>
    /// <remarks> 
    /// Functionally, it is simply a reference to another <see cref="BaseProperty"/>. Those who manually 
    /// instantiate this class should remember to call <see cref="BeginInit"/> before setting the first
    /// property and <see cref="EndInit"/> after setting the last property of the object.
    /// </remarks>
    public sealed class Argument : ISupportInitialize
    {
        #region Constructor

        /// <summary>
        /// Default constructor needed for XAML deserialization.
        /// </summary>
        public Argument()
        {
            Separator = String.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Name of the <see cref="BaseProperty"/> this argument refers to. 
        /// </summary>
        /// <remarks>
        /// Its value must point to a valid <see cref="BaseProperty"/>. This field is mandatory and culture invariant.
        /// </remarks>
        public string Property
        {
            get;
            set;
        }

        /// <summary>
        /// Tells if the <see cref="BaseProperty"/> pointed to by <see cref="Property"/> must be defined for the definition
        /// of the <see cref="BaseProperty"/> owning this argument to make sense.
        /// </summary>
        /// <remarks> 
        /// This field is optional and is set to <c>false</c> by default.
        /// </remarks>
        public bool IsRequired
        {
            get;
            set;
        }

        /// <summary>
        /// The string used to separate this argument value from the parent <see cref="BaseProperty"/> switch in the command line.
        /// </summary>
        /// <remarks>
        /// This field is optional and culture invariant.
        /// </remarks>
        public string Separator
        {
            get;
            set;
        }

        #endregion

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
