// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains only static methods, which are useful throughout many
    /// of the XMake classes and don't really belong in any specific class.   
    /// </summary>
    internal static class MetadataConversionUtilities
    {
        /// <summary>
        /// Convert a task item metadata to bool. Throw an exception if the string is badly formed and can't
        /// be converted.
        /// 
        /// If the metadata is not found, then set metadataFound to false and then return false.
        /// </summary>
        /// <param name="item">The item that contains the metadata.</param>
        /// <param name="itemMetadataName">The name of the metadata.</param>
        /// <param name="metadataFound">Receives true if the metadata was found, false otherwise.</param>
        /// <returns>The resulting boolean value.</returns>
        internal static bool TryConvertItemMetadataToBool
            (
                ITaskItem item,
                string itemMetadataName,
                out bool metadataFound
            )
        {
            string metadataValue = item.GetMetadata(itemMetadataName);
            if (string.IsNullOrEmpty(metadataValue))
            {
                metadataFound = false;
                return false;
            }
            metadataFound = true;

            try
            {
                return Microsoft.Build.Shared.ConversionUtilities.ConvertStringToBool(metadataValue);
            }
            catch (System.ArgumentException e)
            {
                throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("General.InvalidAttributeMetadata", item.ItemSpec, itemMetadataName, metadataValue, "bool"), e);
            }
        }

        /// <summary>
        /// Convert a task item metadata to bool. Throw an exception if the string is badly formed and can't
        /// be converted.
        /// 
        /// If the attribute is not found, then return false.
        /// </summary>
        /// <param name="item">The item that contains the metadata.</param>
        /// <param name="itemMetadataName">The name of the metadata.</param>
        /// <returns>The resulting boolean value.</returns>
        internal static bool TryConvertItemMetadataToBool
            (
                ITaskItem item,
                string itemMetadataName
            )
        {
            bool metadataFound;
            return TryConvertItemMetadataToBool(item, itemMetadataName, out metadataFound);
        }
    }
}
