using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ProjectFromString : IDisposable
    {
        public Project Project { get; init; }

        private XmlReader _reader;

        public ProjectFromString(string s)
            : this(s, null, null)
        {
        }

        public ProjectFromString(string s, IDictionary<string, string> globalProperties, string toolsVersion)
            : this(s, globalProperties, toolsVersion, ProjectCollection.GlobalProjectCollection)
        {
        }

        public ProjectFromString(string s, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection collection, ProjectLoadSettings loadSettings = ProjectLoadSettings.Default)
        : this(s, globalProperties, toolsVersion, null, collection, loadSettings)
        {
        }

        public ProjectFromString(string s, IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
        {
            _reader = XmlReader.Create(new StringReader(s));
            Project = new(_reader, globalProperties, toolsVersion, subToolsetVersion, projectCollection, loadSettings);
        }

        public void Dispose()
        {
            ((IDisposable)_reader).Dispose();
        }
    }

    public class ProjectRootElementFromString : IDisposable
    {
        public ProjectRootElement Project { get; init; }

        private XmlReader _reader;

        public ProjectRootElementFromString(string s)
            : this(s, ProjectCollection.GlobalProjectCollection)
        {
        }

        public ProjectRootElementFromString(string s, ProjectCollection projectCollection)
            : this(s, projectCollection, false)
        {
        }

        public ProjectRootElementFromString(string s, ProjectCollection projectCollection, bool preserveFormatting)
        {
            _reader = XmlReader.Create(new StringReader(s));

            Project = ProjectRootElement.Create(_reader, projectCollection, preserveFormatting);
        }

        public ProjectRootElementFromString(string s, ProjectCollection projectCollection, bool isExplicitlyLoaded, bool preserveFormatting)
        {
            _reader = XmlReader.Create(new StringReader(s));

            Project = new ProjectRootElement(_reader, projectCollection.ProjectRootElementCache, isExplicitlyLoaded, preserveFormatting);
        }

        public void Dispose()
        {
            ((IDisposable)_reader).Dispose();
        }
    }
}
