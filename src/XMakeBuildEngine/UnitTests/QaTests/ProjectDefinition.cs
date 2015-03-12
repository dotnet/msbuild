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
        private string initialTargets;
        
        /// <summary>
        /// Default targets a project should have
        /// </summary>
        private string defaultTargets;

        /// <summary>
        /// Tools version specified in the project file
        /// </summary>
        private string toolsVersion;

        /// <summary>
        /// Project file name
        /// </summary>
        private string filename;

        /// <summary>
        /// If a real instance of MSBuild project should be created
        /// </summary>
        private bool createMSBuildProject;

        /// <summary>
        /// XMLDocument representation of the project file
        /// </summary>
        private XmlDocumentWithLocation projectXmlDocument;

        /// <summary>
        /// Project XML
        /// </summary>
        private XmlElement projectRootElement;

        /// <summary>
        /// List of targets that have been added to this definition
        /// </summary>
        private Dictionary<string, TargetDefinition> targets;

        /// <summary>
        /// Project definition to use which is specified by the test
        /// </summary>
        private ProjectInstance msbuildProjectInstance;

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
            this.initialTargets = initialTargets;
            this.defaultTargets = defaultTargets;
            this.toolsVersion = toolsVersion;
            this.filename = filename;
            this.createMSBuildProject = createMSBuildProject;
            this.projectXmlDocument = new XmlDocumentWithLocation();
            this.targets = new Dictionary<string, TargetDefinition>();
            this.projectRootElement = this.projectXmlDocument.CreateElement("Project", @"http://schemas.microsoft.com/developer/msbuild/2003");
            GenerateProjectRootElement();
        }

        /// <summary>
        /// Add a new target to the project
        /// </summary>
        public void AddTarget(TargetDefinition target)
        {
            this.projectRootElement.AppendChild(target.FinalTargetXmlElement);
            this.targets.Add(target.Name, target);
        }

        /// <summary>
        /// Generates a project object of the elements set so forth in this object. This returns the new MSBuild project instance
        /// </summary>
        public ProjectInstance GetMSBuildProjectInstance()
        {
            if (!this.createMSBuildProject)
            {
                return null;
            }

            if (this.msbuildProjectInstance != null)
            {
                return this.msbuildProjectInstance;
            }

            CreateDefaultTarget();
            ProjectRootElement pXml = ProjectRootElement.Open(this.projectXmlDocument);
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
                this.msbuildProjectInstance = value;
            }
        }

        /// <summary>
        /// XMLDocument reprenstation of the project xml content
        /// </summary>
        public XmlDocument ProjectXmlDocument
        {
            get
            {
                return this.projectXmlDocument;
            }
        }

        /// <summary>
        /// Project filename
        /// </summary>
        public string Filename
        {
            get
            {
                return this.filename;
            }
        }

        /// <summary>
        /// List of targets in this definition
        /// </summary>
        public Dictionary<string, TargetDefinition> TargetsCollection
        {
            get
            {
                return this.targets;
            }
        }

        /// <summary>
        /// Default Targets for a project
        /// </summary>
        public string DefaultTargets
        {
            get
            {
                return this.defaultTargets;
            }
            set
            {
                this.defaultTargets = value;
            }
        }

        /// <summary>
        /// Initial Targets for a project
        /// </summary>
        public string InitialTargets
        {
            get
            {
                return this.initialTargets;
            }
            set
            {
                this.initialTargets = value;
            }
        }

        /// <summary>
        /// If MSBuild project object is to be created
        /// </summary>
        public bool CreateMSBuildProject
        {
            get
            {
                return this.createMSBuildProject;
            }
            set
            {
                this.createMSBuildProject = value;
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Create a default target in the project file if one is not already there
        /// </summary>
        private void CreateDefaultTarget()
        {
            if (this.projectRootElement.GetElementsByTagName("Target") == null || this.projectRootElement.GetElementsByTagName("Target").Count == 0)
            {
                CreateDefaultFirstTarget();
            }
        }

        /// <summary>
        /// Create XML Element representing a target
        /// </summary>
        private void GenerateProjectRootElement()
        {
            
            this.projectRootElement.SetAttribute("xmlns", @"http://schemas.microsoft.com/developer/msbuild/2003");

            if (this.defaultTargets != null)
            {
                this.projectRootElement.SetAttribute("DefaultTargets", this.defaultTargets);
            }

            if (this.initialTargets != null)
            {
                this.projectRootElement.SetAttribute("InitialTargets", this.initialTargets);
            }

            if (this.toolsVersion != null)
            {
                this.projectRootElement.SetAttribute("ToolsVersion", this.toolsVersion);
            }

            XmlElement propertyGroupElement = this.projectXmlDocument.CreateElement("PropertyGroup", @"http://schemas.microsoft.com/developer/msbuild/2003");
            XmlNode propertyGroup = this.projectRootElement.AppendChild(propertyGroupElement as XmlNode);
            XmlElement propertyElement = this.projectXmlDocument.CreateElement("GlobalConfigurationName", @"http://schemas.microsoft.com/developer/msbuild/2003");
            propertyElement.InnerXml = this.filename + ":$(ConfigurationId)";
            propertyGroup.AppendChild(propertyElement as XmlNode);
            this.projectXmlDocument.AppendChild(this.projectRootElement as XmlNode);
        }

        /// <summary>
        /// Create a default target in the project file if there are no existing targets in the project file
        /// </summary>
        private void CreateDefaultFirstTarget()
        {
            TargetDefinition target = new TargetDefinition(RequestDefinition.defaultTargetName, this.projectXmlDocument);
            this.AddTarget(target);
            this.projectRootElement.AppendChild(target.FinalTargetXmlElement);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Clear lists created
        /// </summary>
        public void Dispose()
        {
            this.targets.Clear();
        }

        #endregion
    }
}

