// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Indicates to the expander what exactly it should expand.
/// </summary>
[Flags]
internal enum ExpanderOptions
{
    /// <summary>
    /// Invalid
    /// </summary>
    Invalid = 0x0,

    /// <summary>
    /// Expand bare custom metadata, like %(foo), but not built-in
    /// metadata, such as %(filename) or %(identity)
    /// </summary>
    ExpandCustomMetadata = 0x1,

    /// <summary>
    /// Expand bare built-in metadata, such as %(filename) or %(identity)
    /// </summary>
    ExpandBuiltInMetadata = 0x2,

    /// <summary>
    /// Expand all bare metadata
    /// </summary>
    ExpandMetadata = ExpandCustomMetadata | ExpandBuiltInMetadata,

    /// <summary>
    /// Expand only properties
    /// </summary>
    ExpandProperties = 0x4,

    /// <summary>
    /// Expand only item list expressions
    /// </summary>
    ExpandItems = 0x8,

    /// <summary>
    /// If the expression is going to not be an empty string, break
    /// out early
    /// </summary>
    BreakOnNotEmpty = 0x10,

    /// <summary>
    /// When an error occurs expanding a property, just leave it unexpanded.
    /// </summary>
    /// <remarks>
    /// This should only be used in cases where property evaluation isn't critical, such as when attempting to log a
    /// message with a best effort expansion of a string, or when discovering partial information during lazy evaluation.
    /// </remarks>
    LeavePropertiesUnexpandedOnError = 0x20,

    /// <summary>
    /// When an expansion occurs, truncate it to Expander.DefaultTruncationCharacterLimit or Expander.DefaultTruncationItemLimit.
    /// </summary>
    Truncate = 0x40,

    /// <summary>
    /// Issues build message if item references unqualified or qualified metadata odf self - as this can lead to unintended expansion and
    ///  cross-combination of other items.
    /// More info: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-batching#item-batching-on-self-referencing-metadata
    /// </summary>
    LogOnItemMetadataSelfReference = 0x80,

    /// <summary>
    /// Expand only properties and then item lists
    /// </summary>
    ExpandPropertiesAndItems = ExpandProperties | ExpandItems,

    /// <summary>
    /// Expand only bare metadata and then properties
    /// </summary>
    ExpandPropertiesAndMetadata = ExpandMetadata | ExpandProperties,

    /// <summary>
    /// Expand only bare custom metadata and then properties
    /// </summary>
    ExpandPropertiesAndCustomMetadata = ExpandCustomMetadata | ExpandProperties,

    /// <summary>
    /// Expand bare metadata, then properties, then item expressions
    /// </summary>
    ExpandAll = ExpandMetadata | ExpandProperties | ExpandItems
}
