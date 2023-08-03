// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.Tasks
{
    public class TransformAppSettingsTests
    {

        private static readonly ITaskItem DefaultConnectionTaskItem = new TaskItem("DefaultConnection", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=defaultDB;Trusted_Connection=True;MultipleActiveResultSets=true" } });
        private static readonly ITaskItem CarConnectionTaskItem = new TaskItem("CarConnection", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=CarDB;Trusted_Connection=True;MultipleActiveResultSets=true" } });
        private static readonly ITaskItem PersonConnectionTaskItem = new TaskItem("PersonConnection", new Dictionary<string, string>() { { "Value", @"Server=(localdb)\mssqllocaldb; Database=PersonDb;Trusted_Connection=True;MultipleActiveResultSets=true" } });

        private static readonly List<object[]> testData = new List<object[]>
        {
            new object[] {new ITaskItem[] { DefaultConnectionTaskItem } },
            new object[] {new ITaskItem[] { DefaultConnectionTaskItem, CarConnectionTaskItem, PersonConnectionTaskItem } }
        };

        public static IEnumerable<object[]> ConnectionStringsData
        {
            get { return testData; }
        }

        [Theory]
        [MemberData(nameof(ConnectionStringsData))]
        public void TransformAppSettings_NoAppSettingsInSourceFolder(ITaskItem[] connectionStringData)
        {
            //Arrange
            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string publishDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(publishDir))
            {
                Directory.CreateDirectory(publishDir);
            }

            ITaskItem[] destinationConnectionStrings = connectionStringData;

            TransformAppSettings task = new TransformAppSettings()
            {
                ProjectDirectory = projectFolder,
                PublishDirectory = publishDir,
                DestinationConnectionStrings = destinationConnectionStrings
            };

            // Act
            bool result = task.TransformAppSettingsInternal();

            //Assert
            Assert.True(result);
            string appSettingsProductionJson = (Path.Combine(publishDir, "appsettings.production.json"));
            Assert.True(File.Exists(appSettingsProductionJson));

            foreach (var eachValue in connectionStringData)
            {
                JToken connectionStringValue = JObject.Parse(File.ReadAllText(appSettingsProductionJson))["ConnectionStrings"][eachValue.ItemSpec];
                Assert.Equal(connectionStringValue.ToString(), eachValue.GetMetadata("Value"));
            }

            if (File.Exists(appSettingsProductionJson))
            {
                File.Delete(appSettingsProductionJson);
            }

            if (Directory.Exists(publishDir))
            {
                Directory.Delete(publishDir, true);
            }
        }


        [Theory]
        [MemberData(nameof(ConnectionStringsData))]
        public void TransformAppSettings_FailsIfPublishDirectoryDoesNotExist(ITaskItem[] connectionStringData)
        {
            //Arrange
            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string publishDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            ITaskItem[] destinationConnectionStrings = connectionStringData;

            TransformAppSettings task = new TransformAppSettings()
            {
                ProjectDirectory = projectFolder,
                PublishDirectory = publishDir,
                DestinationConnectionStrings = destinationConnectionStrings
            };

            // Act
            bool result = task.TransformAppSettingsInternal();

            //Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(ConnectionStringsData))]
        public void TransformAppSettings_OverrideSourceAppSettingsName(ITaskItem[] connectionStringData)
        {
            //Arrange
            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string publishDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(publishDir))
            {
                Directory.CreateDirectory(publishDir);
            }

            ITaskItem[] destinationConnectionStrings = connectionStringData;

            TransformAppSettings task = new TransformAppSettings()
            {
                ProjectDirectory = projectFolder,
                PublishDirectory = publishDir,
                DestinationConnectionStrings = destinationConnectionStrings,
                SourceAppSettingsName = "MyCustomAppSettings.json"

            };

            // Act
            bool result = task.TransformAppSettingsInternal();

            //Assert
            Assert.True(result);
            string appSettingsProductionJson = (Path.Combine(publishDir, $"MyCustomAppSettings.production.json"));
            Assert.True(File.Exists(appSettingsProductionJson));

            foreach (var eachValue in connectionStringData)
            {
                JToken connectionStringValue = JObject.Parse(File.ReadAllText(appSettingsProductionJson))["ConnectionStrings"][eachValue.ItemSpec];
                Assert.Equal(connectionStringValue.ToString(), eachValue.GetMetadata("Value"));
            }

            if (File.Exists(appSettingsProductionJson))
            {
                File.Delete(appSettingsProductionJson);
            }

            if (Directory.Exists(publishDir))
            {
                Directory.Delete(publishDir, true);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionStringsData))]
        public void TransformAppSettings_OverrideDestinationAppSettingsName(ITaskItem[] connectionStringData)
        {
            //Arrange
            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string publishDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            if (!Directory.Exists(publishDir))
            {
                Directory.CreateDirectory(publishDir);
            }

            ITaskItem[] destinationConnectionStrings = connectionStringData;

            TransformAppSettings task = new TransformAppSettings()
            {
                ProjectDirectory = projectFolder,
                PublishDirectory = publishDir,
                DestinationConnectionStrings = destinationConnectionStrings,
                SourceAppSettingsName = "MyCustomAppSettings.json",
                DestinationAppSettingsName = "NewDestinationAppSettings.json",

            };

            // Act
            bool result = task.TransformAppSettingsInternal();

            //Assert
            Assert.True(result);
            string appSettingsProductionJson = (Path.Combine(publishDir, $"NewDestinationAppSettings.json"));
            Assert.True(File.Exists(appSettingsProductionJson));

            foreach (var eachValue in connectionStringData)
            {
                JToken connectionStringValue = JObject.Parse(File.ReadAllText(appSettingsProductionJson))["ConnectionStrings"][eachValue.ItemSpec];
                Assert.Equal(connectionStringValue.ToString(), eachValue.GetMetadata("Value"));
            }

            if (File.Exists(appSettingsProductionJson))
            {
                File.Delete(appSettingsProductionJson);
            }

            if (Directory.Exists(publishDir))
            {
                Directory.Delete(publishDir, true);
            }
        }
    }
}
