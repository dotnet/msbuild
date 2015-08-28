// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Defines the elements of a project so that we can create a in-memory project
    /// </summary>
    internal class ProjectDefinition : IDisposable
    {
        #region Private Data

        /// <summary>
        /// Initial targets a project should have
        /// </summary>
        private string _initialTargets;

        /// <summary>
        /// Default targets a project should have
        /// </summary>
        private string _defaultTargets;

        /// <summary>
        /// Tools version specified in the project file
        /// </summary>
        private string _toolsVersion;

        /// <summary>
        /// Project file name
        /// </summary>
        private string _filename;

        /// <summary>
        /// If a real instance of MSBuild project should be created
        /// </summary>
        private bool _createMSBuildProject;

        /// <summary>
        /// XMLDocument representation of the project file
        /// </summary>
        private XmlDocumentWithLocation _projectXmlDocument;

        /// <summary>
        /// Project XML
        /// </summary>
        private XmlElement _projectRootElement;

        /// <summary>
        /// List of targets that have been added to this definition
        /// </summary>
        private Dictionary<string, TargetDefinition> _targets;

        /// <summary>
        /// Project definition to use which is specified by the test
        /// </summary>
        private ProjectInstance _msbuildProjectInstance;

        #endregion

        #region Public Method

        /// <summary>
        /// Constructor which set filename only
        /// </summary>

        public ProjectDefinition(string filename)
            : this(filename, null, null, null, true)
        {
        }

        /// <summary>
        /// Constructor which set filename and tools version
        /// </summary>

        public ProjectDefinition(string filename, string toolsversion)
            : this(filename, null, null, toolsversion, true)
        {
        }

        /// <summary>
        /// Constructor allows you to set all the data members
        /// </summary>
        public ProjectDefinition(string filename, string initialTargets, string defaultTargets, string toolsVersion, bool createMSBuildProject)
        {
            _initialTargets = initialTargets;
            _defaultTargets = defaultTargets;
            _toolsVersion = toolsVersion;
            _filename = filename;
            _createMSBuildProject = createMSBuildProject;
            _projectXmlDocument = new XmlDocumentWithLocation();
            _targets = new Dictionary<string, TargetDefinition>();
            _projectRootElement = _projectXmlDocument.CreateElement("Project", @"http://schemas.microsoft.com/developer/msbuild/2003");
            GenerateProjectRootElement();
        }

        /// <summary>
        /// Add a new target to the project
        /// </summary>
        public void AddTarget(TargetDefinition target)
        {
            _projectRootElement.AppendChild(target.FinalTargetXmlElement);
            _targets.Add(target.Name, target);
        }

        /// <summary>
        /// Generates a project object of the elements set so forth in this object. This returns the new MSBuild project instance
        /// </summary>
        public ProjectInstance GetMSBuildProjectInstance()
        {
            if (!_createMSBuildProject)
            {
                return null;
            }

            if (_msbuildProjectInstance != null)
            {
                return _msbuildProjectInstance;
            }

            CreateDefaultTarget();
            ProjectRootElement pXml = ProjectRootElement.Open(_projectXmlDocument);
            Microsoft.Build.Evaluation.Project pDef = new Microsoft.Build.Evaluation.Project(pXml);
            return pDef.CreateProjectInstance();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// If the test wants to use its own project instance
        /// </summary>
        public ProjectInstance MSBuildProjectInstance
        {
            set
            {
                _msbuildProjectInstance = value;
            }
        }

        /// <summary>
        /// XMLDocument reprenstation of the project xml content
        /// </summary>
        public XmlDocument ProjectXmlDocument
        {
            get
            {
                return _projectXmlDocument;
            }
        }

        /// <summary>
        /// Project filename
        /// </summary>
        public string Filename
        {
            get
            {
                return _filename;
            }
        }

        /// <summary>
        /// List of targets in this definition
        /// </summary>
        public Dictionary<string, TargetDefinition> TargetsCollection
        {
            get
            {
                return _targets;
            }
        }

        /// <summary>
        /// Default Targets for a project
        /// </summary>
        public string DefaultTargets
        {
            get
            {
                return _defaultTargets;
            }
            set
            {
                _defaultTargets = value;
            }
        }

        /// <summary>
        /// Initial Targets for a project
        /// </summary>
        public string InitialTargets
        {
            get
            {
                return _initialTargets;
            }
            set
            {
                _initialTargets = value;
            }
        }

        /// <summary>
        /// If MSBuild project object is to be created
        /// </summary>
        public bool CreateMSBuildProject
        {
            get
            {
                return _createMSBuildProject;
            }
            set
            {
                _createMSBuildProject = value;
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Create a default target in the project file if one is not already there
        /// </summary>
        private void CreateDefaultTarget()
        {
            if (_projectRootElement.GetElementsByTagName("Target") == null || _projectRootElement.GetElementsByTagName("Target").Count == 0)
            {
                CreateDefaultFirstTarget();
            }
        }

        /// <summary>
        /// Create XML Element representing a target
        /// </summary>
        private void GenerateProjectRootElement()
        {
            _projectRootElement.SetAttribute("xmlns", @"http://schemas.microsoft.com/developer/msbuild/2003");

            if (_defaultTargets != null)
            {
                _projectRootElement.SetAttribute("DefaultTargets", _defaultTargets);
            }

            if (_initialTargets != null)
            {
                _projectRootElement.SetAttribute("InitialTargets", _initialTargets);
            }

            if (_toolsVersion != null)
            {
                _projectRootElement.SetAttribute("ToolsVersion", _toolsVersion);
            }

            XmlElement propertyGroupElement = _projectXmlDocument.CreateElement("PropertyGroup", @"http://schemas.microsoft.com/developer/msbuild/2003");
            XmlNode propertyGroup = _projectRootElement.AppendChild(propertyGroupElement as XmlNode);
            XmlElement propertyElement = _projectXmlDocument.CreateElement("GlobalConfigurationName", @"http://schemas.microsoft.com/developer/msbuild/2003");
            propertyElement.InnerXml = _filename + ":$(ConfigurationId)";
            propertyGroup.AppendChild(propertyElement as XmlNode);
            _projectXmlDocument.AppendChild(_projectRootElement as XmlNode);
        }

        /// <summary>
        /// Create a default target in the project file if there are no existing targets in the project file
        /// </summary>
        private void CreateDefaultFirstTarget()
        {
            TargetDefinition target = new TargetDefinition(RequestDefinition.defaultTargetName, _projectXmlDocument);
            this.AddTarget(target);
            _projectRootElement.AppendChild(target.FinalTargetXmlElement);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Clear lists created
        /// </summary>
        public void Dispose()
        {
            _targets.Clear();
        }

        #endregion
    }
}

