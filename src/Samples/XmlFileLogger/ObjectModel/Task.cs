// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild task execution.
    /// </summary>
    internal class Task : LogProcessNode
    {
        /// <summary>
        /// The assembly from which the task originated.
        /// </summary>
        private readonly string _fromAssembly;

        /// <summary>
        /// Initializes a new instance of the <see cref="Task"/> class.
        /// </summary>
        /// <param name="name">The name of the task.</param>
        /// <param name="taskStartedEvent">The <see cref="TaskStartedEventArgs"/> instance containing the event data.</param>
        /// <param name="assembly">The assembly from which the task originated.</param>
        public Task(string name, TaskStartedEventArgs taskStartedEvent, string assembly)
        {
            Name = name;
            Id = taskStartedEvent.BuildEventContext.TaskId;
            StartTime = taskStartedEvent.Timestamp;
            _fromAssembly = assembly;
        }

        /// <summary>
        /// Gets or sets the command line arguments.
        /// </summary>
        /// <value>
        /// The command line arguments (if any) for the task).
        /// </value>
        public string CommandLineArguments { get; set; }

        /// <summary>
        /// Saves to task representation to XML XElement.
        /// </summary>
        /// <param name="element">The element to write the task Node to.</param>
        public override void SaveToElement(XElement element)
        {
            var task = new XElement("Task",
                new XAttribute("Name", Name),
                new XAttribute("FromAssembly", _fromAssembly),
                new XAttribute("StartTime", StartTime),
                new XAttribute("EndTime", EndTime));
            element.Add(task);

            WriteChildren<Message>(task, () => new XElement("TaskMessages"));
            WriteChildren<InputParameter>(task, () => new XElement("Parameters"));
            WriteChildren<OutputItem>(task, () => new XElement("OutputItems"));
            WriteChildren<OutputProperty>(task, () => new XElement("OutputProperties"));

            if (!string.IsNullOrEmpty(CommandLineArguments))
            {
                task.Add(new XElement("CommandLineArguments") { Value = CommandLineArguments });
            }
        }

        /// <summary>
        /// Adds a task level parameter (input/output).
        /// </summary>
        /// <param name="parameter">The parameter to add</param>
        public void AddParameter(TaskParameter parameter)
        {
            AddChildNode(parameter);
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
