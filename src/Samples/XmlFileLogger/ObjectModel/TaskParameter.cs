// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Abstract base class for task input / output parameters (can be ItemGroups)
    /// </summary>
    internal abstract class TaskParameter : ILogNode
    {
        protected bool collapseSingleItem;
        protected string itemAttributeName;
        protected readonly List<Item> items = new List<Item>();
        protected readonly string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskParameter"/> class.
        /// </summary>
        /// <param name="message">The message from the logging system.</param>
        /// <param name="prefix">The prefix parsed out (e.g. 'Output Item(s): ').</param>
        /// <param name="collapseSingleItem">If set to <c>true</c>, will collapse the node to a single item when possible.</param>
        /// <param name="itemAttributeName">Name of the item 'Include' attribute.</param>
        protected TaskParameter(string message, string prefix, bool collapseSingleItem = true, string itemAttributeName = "Include")
        {
            this.collapseSingleItem = collapseSingleItem;
            this.itemAttributeName = itemAttributeName;

            foreach (var item in ItemGroupParser.ParseItemList(message, prefix, out name))
            {
                items.Add(item);
            }
        }

        /// <summary>
        /// Saves the task parameter node to XML XElement.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        public void SaveToElement(XElement parentElement)
        {
            XElement element = new XElement(name);
            parentElement.Add(element);

            if (collapseSingleItem && items.Count == 1 && !items[0].Metadata.Any())
            {
                element.Add(items[0].Text);
            }
            else
            {
                foreach (var item in items)
                {
                    item.SaveToElement(element, itemAttributeName, collapseSingleItem);
                }
            }
        }

        /// <summary>
        /// Creates a concrete Task Parameter type based on the message logging message.
        /// </summary>
        /// <param name="message">The message string from the logger.</param>
        /// <param name="prefix">The prefix to the message string.</param>
        /// <returns>Concrete task parameter node.</returns>
        public static TaskParameter Create(string message, string prefix)
        {
            return prefix switch
            {
                XmlFileLogger.OutputItemsMessagePrefix => new OutputItem(message, prefix),
                XmlFileLogger.TaskParameterMessagePrefix => new InputParameter(message, prefix),
                XmlFileLogger.OutputPropertyMessagePrefix => new OutputProperty(message, prefix),
                XmlFileLogger.ItemGroupIncludeMessagePrefix => new ItemGroup(message, prefix, "Include"),
                XmlFileLogger.ItemGroupRemoveMessagePrefix => new ItemGroup(message, prefix, "Remove"),
                _ => throw new UnknownTaskParameterPrefixException(prefix),
            };
        }
    }
}
