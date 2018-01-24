// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Interface defining properties, items, and metadata of interest for a <see cref="BuildRequestData"/>.
    /// </summary>
    public class RequestedProjectState : INodePacketTranslatable
    {
        private List<string> _propertyFilters;
        private IDictionary<string, List<string>> _itemFilters;

        /// <summary>
        /// Properties of interest.
        /// </summary>
        public List<string> PropertyFilters
        {
            get { return _propertyFilters; }
            set { _propertyFilters = value; }
        }

        /// <summary>
        /// Items and metadata of interest.
        /// </summary>
        public IDictionary<string, List<string>> ItemFilters
        {
            get { return _itemFilters; }
            set { _itemFilters = value; }
        }

        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _propertyFilters);
            translator.TranslateDictionary(ref _itemFilters, TranslateString, TranslateMetadataForItem, CreateItemMetadataDictionary);
        }

        private IDictionary<string, List<string>> CreateItemMetadataDictionary(int capacity)
        {
            return new Dictionary<string, List<string>>(capacity, StringComparer.OrdinalIgnoreCase);
        }

        private void TranslateMetadataForItem(ref List<string> list, INodePacketTranslator translator)
        {
            translator.Translate(ref list);
        }

        private void TranslateString(ref string s, INodePacketTranslator translator)
        {
            translator.Translate(ref s);
        }
    }
}
