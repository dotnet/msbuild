// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Markup;

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represents the schema of an enumeration property.
    /// </summary>
    /// <remarks> This class inherits the <see cref="BaseProperty.Default"/> property from the <see cref="BaseProperty"/> class.
    /// That property does not make sense for this property. Use the <see cref="EnumValue.IsDefault"/> property on the
    /// <see cref="EnumValue"/> instead to mark the default value for this property. </remarks>
    [ContentProperty("AdmissibleValues")]
    public sealed class EnumProperty : BaseProperty
    {
        #region Constructor

        /// <summary>
        /// constructor
        /// </summary>
        public EnumProperty()
        {
            AdmissibleValues = new List<EnumValue>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The list of possible values for this property. Must have at least one value.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This has shipped in Framework, which is especially important to keep binary compatible, so we can't change it now")]
        public List<EnumValue> AdmissibleValues
        {
            get;
            set;
        }

        #endregion 

        #region ISupportInitialize Methods

        /// <summary>
        /// See ISupportInitialize.
        /// </summary>
        public override void EndInit()
        {
            base.EndInit();
        }

        #endregion
    }
}
