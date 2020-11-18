// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents the schema of an enumeration property.
    /// </summary>
    /// <remarks> This class inherits the <see cref="BaseProperty.Default"/> property from the <see cref="BaseProperty"/> class.
    /// That property does not make sense for this property. Use the <see cref="EnumValue.IsDefault"/> property on the
    /// <see cref="EnumValue"/> instead to mark the default value for this property. </remarks>
    public sealed class DynamicEnumProperty : BaseProperty
    {
        #region Constructor

        /// <summary>
        /// constructor
        /// </summary>
        public DynamicEnumProperty()
        {
            // Initialize collection properties in this class. This is required for
            // proper deserialization.
            ProviderSettings = new List<NameValuePair>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The provider that produces the list of possible values for this property. Must be set.
        /// </summary>
        public string EnumProvider { get; set; }

        /// <summary>
        /// A provider-specific set of options to pass to the provider.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Concrete collection types required for XAML deserialization")]
        public List<NameValuePair> ProviderSettings { get; set; }

        #endregion 
    }
}
