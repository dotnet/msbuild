// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of an MSBuild project execution.
    /// </summary>
    internal class Project : LogProcessNode
    {
        /// <summary>
        /// The full path to the MSBuild project file for this project.
        /// </summary>
        private string _projectFile;

        /// <summary>
        /// A lookup table mapping of target names to targets. 
        /// Target names are unique to a project and the id is not always specified in the log.
        /// </summary>
        private readonly ConcurrentDictionary<string, Target> _targetNameToTargetMap = new ConcurrentDictionary<string, Target>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class.
        /// </summary>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="e">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        /// <param name="parentPropertyBag">The parent property bag (to check for inherited properties).</param>
        public Project(int projectId, ProjectStartedEventArgs e, PropertyBag parentPropertyBag)
        {
            Properties = new PropertyBag(parentPropertyBag);
            Id = projectId;

            TryUpdate(e);
        }

        /// <summary>
        /// Add the given project as a child to this node.
        /// </summary>
        /// <param name="childProject">The child project to add.</param>
        public void AddChildProject(Project childProject)
        {
            AddChildNode(childProject);
        }

        /// <summary>
        /// Adds a new target node to the project.
        /// </summary>
        /// <param name="targetStartedEventArgs">The <see cref="TargetStartedEventArgs"/> instance containing the event data.</param>
        public void AddTarget(TargetStartedEventArgs targetStartedEventArgs)
        {
            var target = GetOrAddTargetByName(targetStartedEventArgs.TargetName, targetStartedEventArgs);

            if (!string.IsNullOrEmpty(targetStartedEventArgs.ParentTarget))
            {
                var parentTarget = GetOrAddTargetByName(targetStartedEventArgs.ParentTarget);
                parentTarget.AddChildTarget(target);
            }
            else
            {
                AddChildNode(target);
            }
        }

        /// <summary>
        /// Gets the child target by identifier.
        /// </summary>
        /// <remarks>Throws if the child target does not exist</remarks>
        /// <param name="id">The target identifier.</param>
        /// <returns>Target with the given ID</returns>
        public Target GetTargetById(int id)
        {
            return _targetNameToTargetMap.Values.First(t => t.Id == id);
        }

        /// <summary>
        /// Writes the project and its children to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        public override void SaveToElement(XElement parentElement)
        {
            // We could be in a situation where we never saw a "Parent" Target. So it's now
            // in our scope but not rooted. This can happen when targets fail to run.
            // Let's just add them back.
            foreach (var orphan in _targetNameToTargetMap.Values.Where(t => t.Id < 0))
            {
                AddChildNode(orphan);
            }

            var element = new XElement("Project",
                new XAttribute("Name", Name.Replace("\"", string.Empty)),
                new XAttribute("StartTime", StartTime),
                new XAttribute("EndTime", EndTime),
                new XAttribute("ProjectFile", _projectFile));

            parentElement.Add(element);

            WriteProperties(element);
            WriteChildren<Message>(element, () => new XElement("ProjectMessageEvents"));
            WriteChildren<Project>(element);
            WriteChildren<Target>(element);
        }

        /// <summary>
        /// Try to update the project data given a project started event. This is useful if the project
        /// was created (e.g. as a parent) before we saw the started event.
        /// <remarks>Does nothing if the data has already been set or the new data is null.</remarks>
        /// </summary>
        /// <param name="projectStartedEventArgs">The <see cref="ProjectStartedEventArgs"/> instance containing the event data.</param>
        public void TryUpdate(ProjectStartedEventArgs projectStartedEventArgs)
        {
            if (Name == null && projectStartedEventArgs != null)
            {
                StartTime = projectStartedEventArgs.Timestamp;
                Name = projectStartedEventArgs.Message;
                _projectFile = projectStartedEventArgs.ProjectFile;

                if (projectStartedEventArgs.GlobalProperties != null)
                {
                    Properties.AddProperties(projectStartedEventArgs.GlobalProperties);
                }
                if (projectStartedEventArgs.Properties != null)
                {
                    Properties.AddProperties(projectStartedEventArgs.Properties.Cast<DictionaryEntry>());
                }
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

        /// <summary>
        /// Gets a child target by name. If the target doesn't exist a stub will be created.
        /// </summary>
        /// <param name="targetName">Name of the target.</param>
        /// <param name="e">The <see cref="TargetStartedEventArgs"/> instance containing the event data, if any.</param>
        /// <returns>Target node</returns>
        private Target GetOrAddTargetByName(string targetName, TargetStartedEventArgs e = null)
        {
            Target result = _targetNameToTargetMap.GetOrAdd(targetName, key=> new Target(key, e));

            if (e != null)
            {
                result.TryUpdate(e);
            }

            return result;
        }
    }
}
