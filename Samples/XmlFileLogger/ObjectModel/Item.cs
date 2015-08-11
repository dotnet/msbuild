// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an item/property with associated metadata (if any).
    /// </summary>
    internal class Item
    {
        /// <summary>
        /// The metadata associated with this Item
        /// </summary>
        private readonly List<KeyValuePair<string, string>> _metadata = new List<KeyValuePair<string, string>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Item"/> class.
        /// </summary>
        /// <param name="text">The text value of the item (e.g. file name).</param>
        public Item(string text)
        {
            Text = text;
        }

        /// <summary>
        /// Gets the text value of the item.
        /// </summary>
        /// <value>
        /// The text.
        /// </value>
        public string Text { get; private set; }

        /// <summary>
        /// Gets the (non null/empty) metadata.
        /// </summary>
        /// <value>
        /// The metadata.
        /// </value>
        public IEnumerable<KeyValuePair<string, string>> Metadata
        {
            get { return _metadata; }
        }

        /// <summary>
        /// Adds the metadata to the item.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        public void AddMetadata(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _metadata.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        /// <summary>
        /// Writes the item to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        /// <param name="xmlAttributeName">Name of the item 'Include' attribute.</param>
        /// <param name="collapseSingleItem">If set to <c>true</c>, will collapse the node to a single item when possible.</param>
        public void SaveToElement(XElement parentElement, string xmlAttributeName, bool collapseSingleItem)
        {
            var element = new XElement("Item");
            parentElement.Add(element);

            if (Metadata.Any() || !collapseSingleItem)
            {
                element.Add(new XAttribute(xmlAttributeName, Text));
                foreach (var metadataItem in Metadata)
                {
                    var metadataElement = new XElement(metadataItem.Key);
                    metadataElement.Add(metadataItem.Value);
                    element.Add(metadataElement);
                }
            }
            else
            {
                element.Add(new XText(Text));
            }
        }
    }
}
