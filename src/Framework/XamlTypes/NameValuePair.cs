// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

#nullable disable

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents a name-value pair. The name cannot be null or empty.
    /// </summary>
    public class NameValuePair
    {
        #region Constructor

        /// <summary>
        /// Default constructor needed for
        /// </summary>
        public NameValuePair()
        {
        }

        #endregion // Constructor

        #region Properties

        /// <summary>
        /// The name.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// The value.
        /// </summary>
        [Localizable(true)]
        public string Value
        {
            get;
            set;
        }

        #endregion // Properties
    }
}
