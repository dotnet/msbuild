// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    private readonly ref partial struct PropertyExpander
    {
        /// <summary>
        ///  Describes characteristics discovered while scanning a property reference.
        /// </summary>
        [Flags]
        private enum PropertyReferenceFlags
        {
            /// <summary>
            ///  The property body contains no syntax that requires specialized expansion.
            /// </summary>
            None = 0,

            /// <summary>
            ///  The matching closing parenthesis is the final character in the expression.
            /// </summary>
            EndsExpression = 1 << 0,

            /// <summary>
            ///  The property body contains syntax that may identify a property function.
            /// </summary>
            PotentialPropertyFunction = 1 << 1,

            /// <summary>
            ///  The property body contains syntax that may identify a registry property.
            /// </summary>
            PotentialRegistryFunction = 1 << 2,

            /// <summary>
            ///  The property body may require property-function or registry-property expansion.
            /// </summary>
            PotentialSpecialSyntax = PotentialPropertyFunction | PotentialRegistryFunction,
        }
    }
}
