// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents the schema of a string property.
    /// </summary>
    public sealed class StringProperty : BaseProperty
    {
        #region Properties

        /// <summary>
        /// Qualifies this string property to give it a more specific classification.
        /// </summary>
        /// <remarks>
        /// The value this field is set to, must be understood by the consumer of this field
        /// (normally a UI renderer).
        /// </remarks>
        /// <example> The value of this property can be set to, say, "File", "Folder", "CarModel" etc. to specify
        /// if this is a file path, folder path, car model name etc. </example>
        public string Subtype
        {
            get;
            set;
        }

        #endregion
    }
}
