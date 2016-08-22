using Microsoft.DotNet.TestFramework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    /// <summary>
    /// Used to build up test scenario project.jsons without needing to add a new test asset.
    /// </summary>
    public class ProjectJsonBuilder
    {
        private static readonly string s_defaultProjectJsonTestAsset = "TestAppWithRuntimeOptions";

        private TestAssetsManager _testAssetsManager;
        private JObject _projectJson;

        private bool _baseDefined = false;
        private bool _baseProjectDirectory;
        
        public ProjectJsonBuilder(TestAssetsManager testAssetsManager)
        {
            _testAssetsManager = testAssetsManager;
        }

        public string SaveToDisk(string outputDirectory)
        {
            EnsureBaseIsSet();

            var projectPath = Path.Combine(outputDirectory, "project.json");
            File.WriteAllText(projectPath, _projectJson.ToString());
            return projectPath;
        }

        public JObject Build()
        {
            EnsureBaseIsSet();
            return _projectJson;
        }

        public ProjectJsonBuilder FromDefaultBase()
        {
            return FromTestAssetBase(s_defaultProjectJsonTestAsset);
        }

        public ProjectJsonBuilder FromTestAssetBase(string testAssetName)
        {
            var testProjectDirectory = _testAssetsManager.CreateTestInstance(testAssetName).Path;
            var testProject = Path.Combine(testProjectDirectory, "project.json");

            SetBase(JObject.Parse(File.ReadAllText(testProject)));

            return this;
        }

        public ProjectJsonBuilder FromStringBase(string jsonString)
        {
            SetBase(JObject.Parse(jsonString));
            return this;
        }

        public ProjectJsonBuilder FromEmptyBase()
        { 
            SetBase(new JObject());
            return this;
        }

        public ProjectJsonBuilder WithCustomProperty(string propertyName, Dictionary<string, string> value)
        {
            EnsureBaseIsSet();

            _projectJson[propertyName] = JObject.FromObject(value);

            return this;
        }

        public ProjectJsonBuilder WithCustomProperty(string propertyName, string value)
        {
            EnsureBaseIsSet();

            _projectJson[propertyName] = value;

            return this;
        }

        public ProjectJsonBuilder WithCustomProperty(string propertyName, string[] value)
        {
            EnsureBaseIsSet();

            _projectJson[propertyName] = JArray.FromObject(value);

            return this;
        }

        private void SetBase(JObject project)
        {
            if (_baseDefined)
            {
                throw new Exception("Base was already defined.");
            }
            _baseDefined = true;

            _projectJson = project;
        }

        private void EnsureBaseIsSet()
        {
            if (!_baseDefined)
            {
                throw new Exception("Cannot build without base set");
            }
        }
    }
}
