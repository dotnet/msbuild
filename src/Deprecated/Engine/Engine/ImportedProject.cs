// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class represents a project that has been imported into another project with the &lt;Import&gt; tag.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class ImportedProject
    {

        /// <summary>
        /// Creates an instance of this class for the specified project file.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="projectFile">The imported project file.</param>
        /// <exception cref="XmlException">Thrown when the project file contains invalid XML.</exception>
        internal ImportedProject(string projectFile)
        {
            projectXml = new XmlDocument();
            // NOTE: XmlDocument.Load() may throw an XmlException
            projectXml.Load(projectFile);

            FileInfo projectFileInfo = new FileInfo(projectFile);
            lastWriteTime = projectFileInfo.LastWriteTime;
            fileSize = projectFileInfo.Length;
        }

        /// <summary>
        /// Gets the XML for the imported project.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>The parsed XML from the imported project file.</value>
        internal XmlDocument Xml
        {
            get
            {
                return projectXml;
            }
        }

        /// <summary>
        /// Checks if the imported project file has changed on disk.
        /// </summary>
        /// <remarks>
        /// PERF NOTE: This method deliberately requires the project file path to be passed in, to avoid caching the path string
        /// as part of this class. Alternatively, the path could be retrieved from the XmlDocument.BaseURI property (see the
        /// <see cref="XmlUtilities.GetXmlNodeFile(XmlNode, string)"/> method), but again that would be unnecessary work, since the path is
        /// available to the calling code.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="projectFile">The imported project file.</param>
        /// <returns>true, if project file has changed on disk</returns>
        internal bool HasChangedOnDisk(string projectFile)
        {
            FileInfo projectFileInfo = new FileInfo(projectFile);

            return ((lastWriteTime != projectFileInfo.LastWriteTime) || (fileSize != projectFileInfo.Length));
        }

        // the XML for the imported project
        private XmlDocument projectXml;

        // the last time the imported project file was modified
        private DateTime lastWriteTime;

        // the size of the imported project file
        private long fileSize;
    }
}
