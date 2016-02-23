// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.Tasks.Unittest
{
    public class GenerateBindingRedirectsTests
    {
        /// <summary>
        /// In this case,
        /// - A valid redirect information is provided for <see cref="GenerateBindingRedirects"/> task.
        /// Expected:
        /// - Task should create a target app.config with specified redirect information.
        /// Rationale:
        /// - The only goal for <see cref="GenerateBindingRedirects"/> task is to add specified redirects to the output app.config.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void TargetAppConfigShouldContainsBindingRedirects()
        {
            // Arrange
            // Current appConfig is empty
            string appConfigFile = WriteAppConfigRuntimeSection(string.Empty);
            TaskItemMock redirect = new TaskItemMock("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, redirect);

            // Assert
            Assert.True(redirectResults.ExecuteResult);
            Assert.Contains("<assemblyIdentity name=\"System\" publicKeyToken=\"b77a5c561934e089\" culture=\"neutral\" />", redirectResults.TargetAppConfigContent);
            Assert.Contains("newVersion=\"40.0.0.0\"", redirectResults.TargetAppConfigContent);
        }

        /// <summary>
        /// In this case,
        /// - A valid redirect information is provided for <see cref="GenerateBindingRedirects"/> task and app.config is not empty.
        /// Expected:
        /// - Task should create a target app.config with specified redirect information.
        /// Rationale:
        /// - The only goal for <see cref="GenerateBindingRedirects"/> task is to add specified redirects to the output app.config.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void TargetAppConfigShouldContainsBindingRedirectsFromAppConfig()
        {
            // Arrange
            string appConfigFile = WriteAppConfigRuntimeSection(
@"<assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
  <dependentAssembly>
    <assemblyIdentity name=""MyAssembly""
                  publicKeyToken = ""14a739be0244c389""
                  culture = ""Neutral""/>
    <bindingRedirect oldVersion= ""1.0.0.0""
                  newVersion = ""5.0.0.0"" />
  </dependentAssembly>
</assemblyBinding>");

            TaskItemMock redirect = new TaskItemMock("MyAssembly, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='14a739be0244c389'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, redirect);

            // Assert
            Assert.Contains("MyAssembly", redirectResults.TargetAppConfigContent);
            Assert.Contains("<bindingRedirect oldVersion=\"0.0.0.0-40.0.0.0\" newVersion=\"40.0.0.0\"", redirectResults.TargetAppConfigContent);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in with two dependentAssembly elements
        /// Expected:
        /// - Both redirects appears in the output app.config
        /// Rationale:
        /// - assemblyBinding could have more than one dependentAssembly elements and <see cref="GenerateBindingRedirects"/>
        ///   should respect that.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void GenerateBindingRedirectsFromTwoDependentAssemblySections()
        {
            // Arrange
            string appConfigFile = WriteAppConfigRuntimeSection(
@"<loadFromRemoteSources enabled=""true""/>
  <assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"" >
    <dependentAssembly>
      <assemblyIdentity name=""Microsoft.ServiceBus"" publicKeyToken =""31bf3856ad364e35"" culture =""neutral"" />
      <bindingRedirect oldVersion=""2.0.0.0-3.0.0.0"" newVersion =""2.2.0.0"" />
    </dependentAssembly>
    <probing privatePath=""VSO14"" />
    <dependentAssembly>
      <assemblyIdentity name=""System.Web.Http"" publicKeyToken =""31bf3856ad364e35"" culture =""neutral"" />
      <bindingRedirect oldVersion=""4.0.0.0-6.0.0.0"" newVersion =""4.0.0.0"" />
    </dependentAssembly>
    <dependentAssembly>
      <assemblyIdentity name=""Microsoft.TeamFoundation.Common"" publicKeyToken =""b03f5f7f11d50a3a"" culture =""neutral"" />
      <codeBase version=""11.0.0.0"" href =""Microsoft.TeamFoundation.Common.dll"" />
      <codeBase version=""14.0.0.0"" href =""VSO14\Microsoft.TeamFoundation.Common.dll"" />
    </dependentAssembly>
  </assemblyBinding>");

            TaskItemMock serviceBusRedirect = new TaskItemMock("Microsoft.ServiceBus, Version=2.0.0.0, Culture=Neutral, PublicKeyToken='31bf3856ad364e35'", "41.0.0.0");
            TaskItemMock webHttpRedirect = new TaskItemMock("System.Web.Http, Version=4.0.0.0, Culture=Neutral, PublicKeyToken='31bf3856ad364e35'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, serviceBusRedirect, webHttpRedirect);

            // Assert
            Assert.True(redirectResults.ExecuteResult);
            // Naive check that target app.config contains custom redirects.
            // Output config should have max versions for both serviceBus and webhttp assemblies.
            Assert.Contains(string.Format(CultureInfo.InvariantCulture, "oldVersion=\"0.0.0.0-{0}\"", serviceBusRedirect.MaxVersion), redirectResults.TargetAppConfigContent);
            Assert.Contains(string.Format(CultureInfo.InvariantCulture, "newVersion=\"{0}\"", serviceBusRedirect.MaxVersion), redirectResults.TargetAppConfigContent);

            Assert.Contains(string.Format(CultureInfo.InvariantCulture, "oldVersion=\"0.0.0.0-{0}\"", webHttpRedirect.MaxVersion), redirectResults.TargetAppConfigContent);
            Assert.Contains(string.Format(CultureInfo.InvariantCulture, "newVersion=\"{0}\"", webHttpRedirect.MaxVersion), redirectResults.TargetAppConfigContent);

            XElement targetAppConfig = XElement.Parse(redirectResults.TargetAppConfigContent);
            Assert.Equal(1,
                targetAppConfig.Descendants()
                    .Count(e => e.Name.LocalName.Equals("assemblyBinding", StringComparison.OrdinalIgnoreCase)));
            // "Binding redirects should not add additional assemblyBinding sections into the target app.config: " + targetAppConfig

            // Log file should contains a warning when GenerateBindingRedirects updates existing app.config entries
            redirectResults.Engine.AssertLogContains("MSB3836");
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has dependentAssembly section with probing element but without 
        ///   assemblyIdentity or bindingRedirect elements.
        /// Expected:
        /// - No warning
        /// Rationale:
        /// - In initial implementation such app.config was considered invalid and MSB3835 was issued.
        ///   But due to MSDN documentation, dependentAssembly could have only probing element without any other elements inside.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void AppConfigWithProbingPathAndWithoutDependentAssemblyShouldNotProduceWarningsBug1161241()
        {
            // Arrange
            string appConfigFile = WriteAppConfigRuntimeSection(
@"<assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
   <probing privatePath = 'bin;bin2\subbin;bin3'/>  
</assemblyBinding>");
            TaskItemMock redirect = new TaskItemMock("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, redirect);

            // Assert
            Assert.Equal(0, redirectResults.Engine.Errors); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log
            Assert.Equal(0, redirectResults.Engine.Warnings); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has empty assemblyBinding section.
        /// Expected:
        /// - No warning
        /// Rationale:
        /// - In initial implementation such app.config was considered invalid and MSB3835 was issued.
        ///   But due to MSDN documentation, dependentAssembly could have only probing element without any other elements inside.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void AppConfigWithEmptyAssemblyBindingShouldNotProduceWarnings()
        {
            // Arrange
            string appConfigFile = WriteAppConfigRuntimeSection(
@"<assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"" appliesTo=""v1.0.3705""> 
</assemblyBinding>");
            TaskItemMock redirect = new TaskItemMock("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, redirect);

            // Assert
            Assert.Equal(0, redirectResults.Engine.Errors); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log);
            Assert.Equal(0, redirectResults.Engine.Warnings); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has dependentAssembly section with assemblyIdentity but without bindingRedirect
        /// Expected:
        /// - No warning
        /// Rationale:
        /// - Due to app.config xsd schema this is a valid configuration.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void DependentAssemblySectionWithoutBindingRedirectShouldNotProduceWarnings()
        {
            // Arrange
            string appConfigFile = WriteAppConfigRuntimeSection(
@"<assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"" appliesTo=""v1.0.3705"">
      <dependentAssembly>
        <assemblyIdentity name=""Microsoft.TeamFoundation.Common"" publicKeyToken =""b03f5f7f11d50a3a"" culture =""neutral"" />
        <codeBase version=""11.0.0.0"" href =""Microsoft.TeamFoundation.Common.dll"" />
        <codeBase version=""14.0.0.0"" href =""VSO14\Microsoft.TeamFoundation.Common.dll"" />
      </dependentAssembly>
 </assemblyBinding>");
            TaskItemMock redirect = new TaskItemMock("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, redirect);

            // Assert
            Assert.Equal(0, redirectResults.Engine.Errors); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log);
            Assert.Equal(0, redirectResults.Engine.Warnings); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in but dependentAssembly element is empty.
        /// Expected:
        /// - MSB3835
        /// Rationale:
        /// - Due to MSDN documentation, assemblyBinding element should always have a dependentAssembly subsection.
        /// </summary>
        [Fact]
        public void AppConfigInvalidIfDependentAssemblyNodeIsEmpty()
        {
            // Construct the app.config.
            string appConfigFile = WriteAppConfigRuntimeSection(
@"<assemblyBinding xmlns=""urn:schemas-microsoft-com:asm.v1"">
  <dependentAssembly>
  </dependentAssembly>
</assemblyBinding>");
            TaskItemMock redirect = new TaskItemMock("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'", "40.0.0.0");

            // Act
            var redirectResults = GenerateBindingRedirects(appConfigFile, redirect);

            // Assert
            redirectResults.Engine.AssertLogContains("MSB3835");
        }

        private static BindingRedirectsExecutionResult GenerateBindingRedirects(string appConfigFile,
            params TaskItemMock[] suggestedRedirects)
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            string outputAppConfig = FileUtilities.GetTemporaryFile();

            GenerateBindingRedirects bindingRedirects = new GenerateBindingRedirects();
            bindingRedirects.BuildEngine = engine;

            bindingRedirects.SuggestedRedirects = suggestedRedirects ?? new ITaskItem[] { };
            bindingRedirects.AppConfigFile = new TaskItem(appConfigFile);
            bindingRedirects.OutputAppConfigFile = new TaskItem(outputAppConfig);

            try
            {
                bool executionResult = bindingRedirects.Execute();
                return new BindingRedirectsExecutionResult
                {
                    ExecuteResult = executionResult,
                    Engine = engine,
                    SourceAppConfigContent = File.ReadAllText(appConfigFile),
                    TargetAppConfigContent = File.ReadAllText(outputAppConfig),
                };
            }
            finally
            {
                CleanupNoThrow(bindingRedirects);
            }
        }

        private static void CleanupNoThrow(GenerateBindingRedirects redirects)
        {
            if (redirects.AppConfigFile != null && redirects.AppConfigFile.ItemSpec != null)
            {
                FileUtilities.DeleteNoThrow(redirects.AppConfigFile.ItemSpec);
            }

            if (redirects.OutputAppConfigFile != null && redirects.OutputAppConfigFile.ItemSpec != null)
            {
                FileUtilities.DeleteNoThrow(redirects.OutputAppConfigFile.ItemSpec);
            }
        }

        private static string WriteAppConfigRuntimeSection(string runtimeSection)
        {
            string formatString =
@"<configuration>
  <runtime>
     {0}
  </runtime>
</configuration>";
            string appConfigContents = string.Format(CultureInfo.InvariantCulture, formatString, runtimeSection);

            string appConfigFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(appConfigFile, appConfigContents);
            return appConfigFile;
        }

        /// <summary>
        /// Helper class that contains execution results for <see cref="GenerateBindingRedirects"/>.
        /// </summary>
        private class BindingRedirectsExecutionResult
        {
            public MockEngine Engine { get; set; }

            public string SourceAppConfigContent { get; set; }

            public string TargetAppConfigContent { get; set; }

            public bool ExecuteResult { get; set; }
        }

        /// <summary>
        /// Mock implementation of the <see cref="ITaskItem"/>.
        /// </summary>
        private class TaskItemMock : ITaskItem
        {
            private readonly string _maxVersion;

            public TaskItemMock(string assemblyName, string maxVersion)
            {
                ((ITaskItem)this).ItemSpec = assemblyName;
                _maxVersion = maxVersion;
            }

            public string AssemblyName { get; private set; }

            public string MaxVersion { get { return _maxVersion; } }

            string ITaskItem.ItemSpec { get; set; }

            ICollection ITaskItem.MetadataNames { get; }

            int ITaskItem.MetadataCount { get; }

            string ITaskItem.GetMetadata(string metadataName)
            {
                return _maxVersion;
            }

            void ITaskItem.SetMetadata(string metadataName, string metadataValue)
            {
                throw new System.NotImplementedException();
            }

            void ITaskItem.RemoveMetadata(string metadataName)
            {
                throw new System.NotImplementedException();
            }

            void ITaskItem.CopyMetadataTo(ITaskItem destinationItem)
            {
                throw new System.NotImplementedException();
            }

            IDictionary ITaskItem.CloneCustomMetadata()
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
