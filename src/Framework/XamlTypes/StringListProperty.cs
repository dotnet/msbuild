// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents the schema of a list-of-strings property. 
    /// </summary>
    /// <remarks>
    /// Note, this represents
    /// a list of strings, not a list of <see cref="StringProperty"/>s.
    /// </remarks>
    public sealed class StringListProperty : BaseProperty
    {
        #region Constructor

        /// <summary>
        /// Default constructor. Needed for property XAML deserialization.
        /// </summary>
        public StringListProperty()
        {
            RendererValueSeparator = ";";
        }

        #endregion

        #region Properties

        /// <summary>
        /// The separator to use in delineating individual values of this string list property
        /// </summary>
        /// <remarks>
        /// For <c>Val1;Val2;Val3</c>, if <c>CommandLineValueSeparator</c> is specified as, say <c>,</c>,
        /// the command line looks like this: <c>/p:val1,val2,val3</c>
        /// If not specified, the command line looks like this: <c>/p:val1 /p:val2 /p:val3</c>
        /// This field is optional.
        /// </remarks>
        public string CommandLineValueSeparator
        {
            get;
            set;
        }

        /// <summary>
        /// Please don't use. This is planned to be deprecated.
        /// </summary>
        public string RendererValueSeparator
        {
            get;
            set;
        }

        /// <summary>
        /// Qualifies this string property to give it a more specific classification.
        /// </summary>
        /// <remarks>
        /// Similar to the <see cref="StringProperty.Subtype"/> property. 
        /// </remarks>
        public string Subtype
        {
            get;
            set;
        }

        #endregion
    }
}
