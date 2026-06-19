// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ItemSpecModifiers = Microsoft.Build.Framework.ItemSpecModifiers;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// A functor that returns the value of the metadata in the match
    /// that is contained in the metadata dictionary it was created with.
    /// </summary>
    private struct MetadataMatchEvaluator
    {
        /// <summary>
        /// Source of the metadata.
        /// </summary>
        private IMetadataTable _metadata;

        /// <summary>
        /// Whether to expand built-in metadata, custom metadata, or both kinds.
        /// </summary>
        private ExpanderOptions _options;

        private IElementLocation _elementLocation;

        private LoggingContext _loggingContext;

        /// <summary>
        /// Constructor taking a source of metadata.
        /// </summary>
        internal MetadataMatchEvaluator(
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext loggingContext)
        {
            _metadata = metadata;
            _options = options & (ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.LogOnItemMetadataSelfReference);
            _elementLocation = elementLocation;
            _loggingContext = loggingContext;

            Assumed.NotEqual(options, ExpanderOptions.Invalid, "Must be expanding metadata of some kind");
        }

        /// <summary>
        /// Expands a single item metadata, which may be qualified with an item type.
        /// </summary>
        internal static string ExpandSingleMetadata(Match itemMetadataMatch, MetadataMatchEvaluator evaluator)
        {
            Assumed.True(itemMetadataMatch.Success, "Need a valid item metadata.");

            string metadataName = itemMetadataMatch.Groups[RegularExpressions.NameGroup].Value;

            string metadataValue = null;

            bool isBuiltInMetadata = ItemSpecModifiers.IsItemSpecModifier(metadataName);

            if (
                (isBuiltInMetadata && ((evaluator._options & ExpanderOptions.ExpandBuiltInMetadata) != 0)) ||
               (!isBuiltInMetadata && ((evaluator._options & ExpanderOptions.ExpandCustomMetadata) != 0)))
            {
                string itemType = null;

                // check if the metadata is qualified with the item type
                if (itemMetadataMatch.Groups[RegularExpressions.ItemSpecificationGroup].Length > 0)
                {
                    itemType = itemMetadataMatch.Groups[RegularExpressions.ItemTypeGroup].Value;
                }

                metadataValue = evaluator._metadata.GetEscapedValue(itemType, metadataName);

                if ((evaluator._options & ExpanderOptions.LogOnItemMetadataSelfReference) != 0 &&
                    evaluator._loggingContext != null &&
                    !string.IsNullOrEmpty(metadataName) &&
                    evaluator._metadata is IItemTypeDefinition itemMetadata &&
                    (string.IsNullOrEmpty(itemType) || string.Equals(itemType, itemMetadata.ItemType, StringComparison.Ordinal)))
                {
                    evaluator._loggingContext.LogComment(MessageImportance.Low, new BuildEventFileInfo(evaluator._elementLocation),
                        "ItemReferencingSelfInTarget", itemMetadata.ItemType, metadataName);
                }

                if (IsTruncationEnabled(evaluator._options) && metadataValue.Length > CharacterLimitPerExpansion)
                {
                    metadataValue = TruncateString(metadataValue);
                }
            }
            else
            {
                // look up the metadata - we may not have a value for it
                metadataValue = itemMetadataMatch.Value;
            }

            return metadataValue;
        }
    }
}
