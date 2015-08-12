// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Base class to represent a log node (e.g. Project, or Target) that can contain child node sub nodes 
    /// and properties defined at that scope. Properties defined will be inherited from the parent if possible.
    /// </summary>
    internal abstract class LogProcessNode : ILogNode
    {
        /// <summary>
        /// The child nodes bucketed by their type. This is for performance iterating through the list by
        /// type as well as to preserve order within the types (List will preserve order).
        /// </summary>
        private readonly Dictionary<Type, List<ILogNode>> _childNodes;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogProcessNode"/> class.
        /// </summary>
        protected LogProcessNode()
        {
            Properties = new PropertyBag();
            _childNodes = new Dictionary<Type, List<ILogNode>>();
        }

        /// <summary>
        /// Gets or sets the name of the node (e.g. name of the project).
        /// </summary>
        /// <value>
        /// The name of the node.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the node.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public int Id { get; protected set; }

        /// <summary>
        /// Gets or sets the time at which MSBuild indicated the node started execution.
        /// </summary>
        /// <value>
        /// The start time.
        /// </value>
        public DateTime StartTime { get; protected set; }

        /// <summary>
        /// Gets or sets the time at which MSBuild indicated the node completed execution.
        /// </summary>
        /// <value>
        /// The end time.
        /// </value>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the properties collection for this node.
        /// </summary>
        /// <value>
        /// The properties.
        /// </value>
        public PropertyBag Properties { get; protected set; }

        /// <summary>
        /// Writes the node to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        public abstract void SaveToElement(XElement parentElement);

        /// <summary>
        /// Adds a generic message type child to the node.
        /// </summary>
        /// <param name="message">The message node to add.</param>
        public void AddMessage(Message message)
        {
            AddChildNode(message);
        }

        /// <summary>
        /// Adds the child node.
        /// </summary>
        /// <typeparam name="T">Generic ILogNode type to add</typeparam>
        /// <param name="childNode">The child node.</param>
        protected void AddChildNode<T>(T childNode) where T : ILogNode
        {
            var type = childNode.GetType();

            if (_childNodes.ContainsKey(type))
            {
                _childNodes[type].Add(childNode);
            }
            else
            {
                _childNodes[type] = new List<ILogNode> { childNode };
            }
        }

        /// <summary>
        /// Gets the child nodes that are of type T.
        /// </summary>
        /// <remarks>Must be exactly of type T, not inherited</remarks>
        /// <typeparam name="T">Generic ILogNode type</typeparam>
        /// <returns></returns>
        protected IEnumerable<T> GetChildrenOfType<T>() where T : ILogNode
        {
            var t = typeof(T);
            return _childNodes.ContainsKey(t) ? _childNodes[t].Cast<T>() : new List<T>();
        }

        /// <summary>
        /// Writes the properties associated with this node to the XML element.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        protected void WriteProperties(XElement parentElement)
        {
            if (Properties.Properties.Count > 0)
            {
                var propElement = new XElement("Properties");
                foreach (var p in Properties.Properties)
                {
                    propElement.Add(new XElement("Property", new XAttribute("Name", p.Key)) { Value = p.Value });
                }

                parentElement.Add(propElement);
            }
        }

        /// <summary>
        /// Writes the children of type T to the XML element.
        /// </summary>
        /// <typeparam name="T">Generic ILogNode type</typeparam>
        /// <param name="parentElement">The root.</param>
        protected void WriteChildren<T>(XElement parentElement) where T : ILogNode
        {
            foreach (var child in GetChildrenOfType<T>())
            {
                child.SaveToElement(parentElement);
            }
        }

        /// <summary>
        /// Writes the children of type T to the XML element and creates a root node if necessary.
        /// </summary>
        /// <typeparam name="T">Generic ILogNode type</typeparam>
        /// <param name="parentElement">The parent element.</param>
        /// <param name="subNodeFactory">Delegate to create a new element to contain children. Will not be called if 
        /// there are no children of the specified type.</param>
        protected void WriteChildren<T>(XElement parentElement, Func<XElement> subNodeFactory) where T : ILogNode
        {
            if (GetChildrenOfType<T>().Any())
            {
                var node = subNodeFactory();
                WriteChildren<T>(node);
                parentElement.Add(node);
            }
        }
    }
}
