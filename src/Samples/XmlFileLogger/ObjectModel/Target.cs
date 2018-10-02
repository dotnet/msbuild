// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild target execution.
    /// </summary>
    internal class Target : LogProcessNode
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Target"/> class.
        /// </summary>
        /// <param name="targetName">Name of the target.</param>
        /// <param name="targetStartedEvent">The <see cref="TargetStartedEventArgs"/> instance containing the target started event data.</param>
        public Target(string targetName, TargetStartedEventArgs targetStartedEvent)
        {
            Id = -1;
            Name = targetName;
            TryUpdate(targetStartedEvent);
        }

        /// <summary>
        /// Writes the project and its children to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        public override void SaveToElement(XElement parentElement)
        {
            var element = new XElement("Target",
                new XAttribute("Name", Name),
                new XAttribute("StartTime", StartTime),
                new XAttribute("EndTime", EndTime));

            parentElement.Add(element);

            WriteProperties(element);
            WriteChildren<Message>(element, () => new XElement("TargetMessages"));
            WriteChildren<ItemGroup>(element, () => new XElement("ItemGroups"));
            WriteChildren<Target>(element);
            WriteChildren<Task>(element);
        }

        /// <summary>
        /// Adds the given target as a child node.
        /// </summary>
        /// <param name="target">The target to add.</param>
        public void AddChildTarget(Target target)
        {
            AddChildNode(target);
        }

        /// <summary>
        /// Adds the given task as a child node.
        /// </summary>
        /// <param name="task">The task to add.</param>
        public void AddChildTask(Task task)
        {
            AddChildNode(task);
        }

        /// <summary>
        /// Gets a child task by identifier.
        /// </summary>
        /// /// <remarks>Throws if the child target does not exist</remarks>
        /// <param name="taskId">The task identifier.</param>
        /// <returns>Task object with the given id.</returns>
        public Task GetTaskById(int taskId)
        {
            return GetChildrenOfType<Task>().First(t => t.Id == taskId);
        }

        /// <summary>
        /// Adds a property key/value pair to the target context.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        public void AddProperty(string key, string value)
        {
            Properties.AddProperty(key, value);
        }

        /// <summary>
        /// Add a discovered ItemGroup list to the node.
        /// </summary>
        /// <param name="itemGroup">The item group to add.</param>
        public void AddItemGroup(ItemGroup itemGroup)
        {
            AddChildNode(itemGroup);
        }

        /// <summary>
        /// Try to update the target data given a target started event. This is useful if the project
        /// was created (e.g. as a parent) before we saw the started event.
        /// </summary>
        /// <remarks>Does nothing if the data has already been set or the new data is null.</remarks>
        /// <param name="e">The <see cref="TargetStartedEventArgs"/> instance containing the event data.</param>
        public void TryUpdate(TargetStartedEventArgs e)
        {
            if (Id < 0 && e != null)
            {
                // = e.Timestamp;
                Id = e.BuildEventContext.TargetId;
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0} - {1}", Id, Name);
        }
    }
}
