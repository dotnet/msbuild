// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private readonly TestAssets _testAssets;
        private JObject _projectJson;

        private bool _baseDefined = false;

        public ProjectJsonBuilder(TestAssets testAssets)
        {
            _testAssets = testAssets;
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

        public ProjectJsonBuilder FromTestAssetBase(string testAssetName)
        {
            var testProjectDirectory = _testAssets.Get(testAssetName)
                            .CreateInstance()
                            .WithSourceFiles()
                            .Root.FullName;
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
