// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    private static partial class ItemExpander
    {
        private static partial class Transforms
        {
            /// <summary>
            /// The type of match we found.
            /// We use this to determine how to build the final output string.
            /// </summary>
            private enum MetadataMatchType
            {
                /// <summary>
                /// No matches found. The result will be empty.
                /// </summary>
                None,

                /// <summary>
                /// An exact full string match, e.g. '%(FullPath)'.
                /// </summary>
                ExactSingle,

                /// <summary>
                /// A single match with surrounding characters, e.g. 'somedir/%(FileName)'.
                /// </summary>
                InexactSingle,

                /// <summary>
                /// Multiple matches found, e.g. '%(FullPath)%(Extension)'.
                /// </summary>
                Multiple,
            }
        }
    }
}
