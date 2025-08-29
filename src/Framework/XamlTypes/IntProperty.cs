﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework.XamlTypes
{
    /// <summary>
    /// Represent the schema of an integer property.
    /// </summary>
    public sealed class IntProperty : BaseProperty
    {
        #region Properties

        /// <summary>
        /// Minimum allowed value for this property. 
        /// </summary>
        /// <remarks>
        /// This field is optional. 
        /// It returns null when this property is not set. The value of this
        /// property must be less than or equal to the <see cref="MaxValue"/>
        /// property (assuming that the latter is defined).
        /// </remarks>
        public int? MinValue
        {
            get;
            set;
        }

        /// <summary>
        /// Maximum allowed value for this property. 
        /// </summary>
        /// <remarks>
        /// This field is optional. 
        /// It returns null when this property is not set. The value of this
        /// property must be greater than or equal to the <see cref="MinValue"/>
        /// property (assuming that the latter is defined).
        /// </remarks>
        public int? MaxValue
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
