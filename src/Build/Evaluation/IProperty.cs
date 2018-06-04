// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An interface representing an object which can act as a property.
    /// </summary>
    internal interface IProperty : IKeyed
    {
        /// <summary>
        /// Name of the property
        /// </summary>
        string Name
        {
            get;
        }

        /// <summary>
        /// Returns the evaluated, unescaped value for the property.
        /// </summary>
        string EvaluatedValue
        {
            get;
        }

        /// <summary>
        /// Returns the evaluated, escaped value for the property
        /// </summary>
        string EvaluatedValueEscaped
        {
            get;
        }
    }
}
