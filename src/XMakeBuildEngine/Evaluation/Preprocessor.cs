// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary>Helper to create a "preprocessed" or "logical" view of an evaluated Project.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Creates a view of an evaluated project's XML as if it had all been loaded from 
    /// a single file, instead of being assembled by pulling in imported files as it actually was.
    /// </summary>
    /// <remarks>
    /// Ideally the result would be buildable on its own, and *usually* this should be the case.
    /// Known cases where it wouldn't be buildable:
    /// -- $(MSBuildThisFile) and similar properties aren't corrected
    /// -- relative path in exists(..) conditions is relative to the imported file
    /// -- same for AssemblyFile on UsingTask
    /// Paths in item includes are relative to the importing project, though.
    /// </remarks>
    internal class Preprocessor
    {
        /// <summary>
        /// Project to preprocess
        /// </summary>
        private Project _project;

        /// <summary>
        /// Table to resolve import tags
        /// </summary>
        private Dictionary<XmlElement, IList<ProjectRootElement>> _importTable;

        /// <summary>
        /// Stack of file paths pushed as we follow imports
        /// </summary>
        private Stack<string> _filePaths = new Stack<string>();

        /// <summary>
        /// Constructor
        /// </summary>
        private Preprocessor(Project project)
        {
            _project = project;

            IList<ResolvedImport> imports = project.Imports;

            _importTable = new Dictionary<XmlElement, IList<ProjectRootElement>>(imports.Count);

            foreach (ResolvedImport entry in imports)
            {
                IList<ProjectRootElement> list;
                if (!_importTable.TryGetValue(entry.ImportingElement.XmlElement, out list))
                {
                    list = new List<ProjectRootElement>();
                    _importTable[entry.ImportingElement.XmlElement] = list;
                }

                list.Add(entry.ImportedProject);
            }
        }

        /// <summary>
        /// Returns an XmlDocument representing the evaluated project's XML as if it all had 
        /// been loaded from a single file, instead of being assembled by pulling in imported files.
        /// </summary>
        internal static XmlDocument GetPreprocessedDocument(Project project)
        {
            Preprocessor preprocessor = new Preprocessor(project);

            XmlDocument result = preprocessor.Preprocess();

            return result;
        }

        /// <summary>
        /// Root of the preprocessing.
        /// </summary>
        private XmlDocument Preprocess()
        {
            XmlDocument outerDocument = _project.Xml.XmlDocument;

            XmlDocument destinationDocument = (XmlDocument)outerDocument.CloneNode(false /* shallow */);

            _filePaths.Push(_project.FullPath);

            if (!String.IsNullOrEmpty(_project.FullPath)) // Ignore in-memory projects
            {
                destinationDocument.AppendChild(destinationDocument.CreateComment("\r\n" + new String('=', 140) + "\r\n" + _project.FullPath.Replace("--", "__") + "\r\n" + new String('=', 140) + "\r\n"));
            }

            CloneChildrenResolvingImports(outerDocument, destinationDocument);

            return destinationDocument;
        }

        /// <summary>
        /// Recursively called method that clones source nodes into nodes in the destination
        /// document.
        /// </summary>
        private void CloneChildrenResolvingImports(XmlNode source, XmlNode destination)
        {
            XmlDocument sourceDocument = source.OwnerDocument ?? (XmlDocument)source;
            XmlDocument destinationDocument = destination.OwnerDocument ?? (XmlDocument)destination;

            foreach (XmlNode child in source.ChildNodes)
            {
                // Only one of <?xml version="1.0" encoding="utf-16"?> and we got it automatically already
                if (child.NodeType == XmlNodeType.XmlDeclaration)
                {
                    continue;
                }

                // If this is not the first <Project> tag
                if (
                    child.NodeType == XmlNodeType.Element &&
                    sourceDocument.DocumentElement == child &&                                      // This is the root element, not some random element named 'Project'
                    destinationDocument.DocumentElement != null &&                                  // Skip <Project> tag from the outer project
                    String.Equals(XMakeElements.project, child.Name, StringComparison.Ordinal)
                   )
                {
                    // But suffix any InitialTargets attribute
                    string outerInitialTargets = destinationDocument.DocumentElement.GetAttribute(XMakeAttributes.initialTargets).Trim();
                    string innerInitialTargets = ((XmlElement)child).GetAttribute(XMakeAttributes.initialTargets).Trim();

                    if (innerInitialTargets.Length > 0)
                    {
                        if (outerInitialTargets.Length > 0)
                        {
                            outerInitialTargets += ";";
                        }

                        destinationDocument.DocumentElement.SetAttribute(XMakeAttributes.initialTargets, outerInitialTargets + innerInitialTargets);
                    }

                    // Also gather any DefaultTargets value if none has been encountered already; put it on the outer <Project> tag
                    string outerDefaultTargets = destinationDocument.DocumentElement.GetAttribute(XMakeAttributes.defaultTargets).Trim();

                    if (outerDefaultTargets.Length == 0)
                    {
                        string innerDefaultTargets = ((XmlElement)child).GetAttribute(XMakeAttributes.defaultTargets).Trim();

                        if (innerDefaultTargets.Trim().Length > 0)
                        {
                            destinationDocument.DocumentElement.SetAttribute(XMakeAttributes.defaultTargets, innerDefaultTargets);
                        }
                    }

                    CloneChildrenResolvingImports(child, destination);
                    continue;
                }

                // Resolve <Import> to 0-n documents and walk into them
                if (child.NodeType == XmlNodeType.Element && String.Equals(XMakeElements.import, child.Name, StringComparison.Ordinal))
                {
                    // To display what the <Import> tag looked like
                    string importCondition = ((XmlElement)child).GetAttribute(XMakeAttributes.condition);
                    string importProject = ((XmlElement)child).GetAttribute(XMakeAttributes.project);

                    IList<ProjectRootElement> resolvedList;
                    if (!_importTable.TryGetValue(child as XmlElement, out resolvedList))
                    {
                        // Import didn't resolve to anything; just display as a comment and move on
                        string closedImportTag = "<Import Project=\"" + importProject + "\"" + ((importCondition.Length > 0) ? " Condition=\"" + importCondition + "\"" : String.Empty) + " />";
                        destination.AppendChild(destinationDocument.CreateComment(closedImportTag));

                        continue;
                    }

                    for (int i = 0; i < resolvedList.Count; i++)
                    {
                        ProjectRootElement resolved = resolvedList[i];
                        XmlDocument innerDocument = resolved.XmlDocument;

                        string importTag = "  <Import Project=\"" + importProject + "\"" + ((importCondition.Length > 0) ? " Condition=\"" + importCondition + "\"" : String.Empty) + ">";
                        destination.AppendChild(destinationDocument.CreateComment("\r\n" + new String('=', 140) + "\r\n" + importTag + "\r\n\r\n" + resolved.FullPath.Replace("--", "__") + "\r\n" + new String('=', 140) + "\r\n"));

                        _filePaths.Push(resolved.FullPath);
                        CloneChildrenResolvingImports(innerDocument, destination);
                        _filePaths.Pop();

                        if (i < resolvedList.Count - 1)
                        {
                            destination.AppendChild(destinationDocument.CreateComment("\r\n" + new String('=', 140) + "\r\n  </Import>\r\n" + new String('=', 140) + "\r\n"));
                        }
                        else
                        {
                            destination.AppendChild(destinationDocument.CreateComment("\r\n" + new String('=', 140) + "\r\n  </Import>\r\n\r\n" + _filePaths.Peek()?.Replace("--", "__") + "\r\n" + new String('=', 140) + "\r\n"));
                        }
                    }

                    continue;
                }

                // Skip over <ImportGroup> into its children
                if (child.NodeType == XmlNodeType.Element && String.Equals(XMakeElements.importGroup, child.Name, StringComparison.Ordinal))
                {
                    // To display what the <ImportGroup> tag looked like
                    string importGroupCondition = ((XmlElement)child).GetAttribute(XMakeAttributes.condition);
                    string importGroupTag = "<ImportGroup" + ((importGroupCondition.Length > 0) ? " Condition=\"" + importGroupCondition + "\"" : String.Empty) + ">";
                    destination.AppendChild(destinationDocument.CreateComment(importGroupTag));

                    CloneChildrenResolvingImports(child, destination);

                    destination.AppendChild(destinationDocument.CreateComment("</" + XMakeElements.importGroup + ">"));

                    continue;
                }

                // Node doesn't need special treatment, clone and append
                XmlNode clone = destinationDocument.ImportNode(child, false /* shallow */); // ImportNode does a clone but unlike CloneNode it works across XmlDocuments

                destination.AppendChild(clone);

                CloneChildrenResolvingImports(child, clone);
            }
        }
    }
}
