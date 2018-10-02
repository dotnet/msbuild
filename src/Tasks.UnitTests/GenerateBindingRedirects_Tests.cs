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
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.Unittest
{
    public class GenerateBindingRedirectsTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;

        public GenerateBindingRedirectsTests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, redirect);

            // Assert
            redirectResults.ExecuteResult.ShouldBeTrue();
            redirectResults.TargetAppConfigContent.ShouldContain("<assemblyIdentity name=\"System\" publicKeyToken=\"b77a5c561934e089\" culture=\"neutral\" />");
            redirectResults.TargetAppConfigContent.ShouldContain("newVersion=\"40.0.0.0\"");
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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, redirect);

            // Assert
            redirectResults.TargetAppConfigContent.ShouldContain("MyAssembly");
            redirectResults.TargetAppConfigContent.ShouldContain("<bindingRedirect oldVersion=\"0.0.0.0-40.0.0.0\" newVersion=\"40.0.0.0\"");
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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, serviceBusRedirect, webHttpRedirect);

            // Assert
            redirectResults.ExecuteResult.ShouldBeTrue();
            // Naive check that target app.config contains custom redirects.
            // Output config should have max versions for both serviceBus and webhttp assemblies.
            redirectResults.TargetAppConfigContent.ShouldContain($"oldVersion=\"0.0.0.0-{serviceBusRedirect.MaxVersion}\"");
            redirectResults.TargetAppConfigContent.ShouldContain($"newVersion=\"{serviceBusRedirect.MaxVersion}\"");

            redirectResults.TargetAppConfigContent.ShouldContain($"oldVersion=\"0.0.0.0-{webHttpRedirect.MaxVersion}\"");
            redirectResults.TargetAppConfigContent.ShouldContain($"newVersion=\"{webHttpRedirect.MaxVersion}\"");

            XElement targetAppConfig = XElement.Parse(redirectResults.TargetAppConfigContent);
            targetAppConfig.Descendants()
                .Count(e => e.Name.LocalName.Equals("assemblyBinding", StringComparison.OrdinalIgnoreCase))
                .ShouldBe(1);
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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, redirect);

            // Assert
            redirectResults.Engine.Errors.ShouldBe(0); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log
            redirectResults.Engine.Warnings.ShouldBe(0); // "Unexpected errors. Engine log: " + redirectResults.Engine.Log
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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, redirect);

            // Assert
            redirectResults.Engine.Errors.ShouldBe(0);
            redirectResults.Engine.Warnings.ShouldBe(0);
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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, redirect);

            // Assert
            redirectResults.Engine.Errors.ShouldBe(0);
            redirectResults.Engine.Warnings.ShouldBe(0);
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
            var redirectResults = GenerateBindingRedirects(appConfigFile, null, redirect);

            // Assert
            redirectResults.Engine.AssertLogContains("MSB3835");
        }

        [Fact]
        public void AppConfigFileNotSavedWhenIdentical()
        {
            string appConfigFile = WriteAppConfigRuntimeSection(string.Empty);
            string outputAppConfigFile = _env.ExpectFile(".config").Path;

            TaskItemMock redirect = new TaskItemMock("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'", "40.0.0.0");

            var redirectResults = GenerateBindingRedirects(appConfigFile, outputAppConfigFile, redirect);

            // Verify it ran correctly
            redirectResults.ExecuteResult.ShouldBeTrue();
            redirectResults.TargetAppConfigContent.ShouldContain("<assemblyIdentity name=\"System\" publicKeyToken=\"b77a5c561934e089\" culture=\"neutral\" />");
            redirectResults.TargetAppConfigContent.ShouldContain("newVersion=\"40.0.0.0\"");

            var oldTimestamp = DateTime.Now.Subtract(TimeSpan.FromDays(30));
            
            File.SetCreationTime(outputAppConfigFile, oldTimestamp);
            File.SetLastWriteTime(outputAppConfigFile, oldTimestamp);

            // Make sure it's old
            File.GetCreationTime(outputAppConfigFile).ShouldBe(oldTimestamp, TimeSpan.FromSeconds(5));
            File.GetLastWriteTime(outputAppConfigFile).ShouldBe(oldTimestamp, TimeSpan.FromSeconds(5));

            // Re-run the task
            var redirectResults2 = GenerateBindingRedirects(appConfigFile, outputAppConfigFile, redirect);

            // Verify it ran correctly and that it's still old
            redirectResults2.ExecuteResult.ShouldBeTrue();
            redirectResults2.TargetAppConfigContent.ShouldContain("<assemblyIdentity name=\"System\" publicKeyToken=\"b77a5c561934e089\" culture=\"neutral\" />");
            redirectResults.TargetAppConfigContent.ShouldContain("newVersion=\"40.0.0.0\"");

            File.GetCreationTime(outputAppConfigFile).ShouldBe(oldTimestamp, TimeSpan.FromSeconds(5));
            File.GetLastWriteTime(outputAppConfigFile).ShouldBe(oldTimestamp, TimeSpan.FromSeconds(5));
        }

        private BindingRedirectsExecutionResult GenerateBindingRedirects(string appConfigFile, string targetAppConfigFile,
            params ITaskItem[] suggestedRedirects)
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            string outputAppConfig = string.IsNullOrEmpty(targetAppConfigFile) ? _env.ExpectFile(".config").Path : targetAppConfigFile;

            GenerateBindingRedirects bindingRedirects = new GenerateBindingRedirects
            {
                BuildEngine = engine,
                SuggestedRedirects = suggestedRedirects ?? new ITaskItem[] { },
                AppConfigFile = new TaskItem(appConfigFile),
                OutputAppConfigFile = new TaskItem(outputAppConfig)
            };


            bool executionResult = bindingRedirects.Execute();

            return new BindingRedirectsExecutionResult
            {
                ExecuteResult = executionResult,
                Engine = engine,
                SourceAppConfigContent = File.ReadAllText(appConfigFile),
                TargetAppConfigContent = File.ReadAllText(outputAppConfig),
                TargetAppConfigFilePath = outputAppConfig
            };
        }

        private string WriteAppConfigRuntimeSection(string runtimeSection)
        {
            string formatString =
@"<configuration>
  <runtime>
     {0}
  </runtime>
</configuration>";
            string appConfigContents = string.Format(formatString, runtimeSection);

            string appConfigFile = _env.CreateFile(".config").Path;
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

            public string TargetAppConfigFilePath { get; set; }
        }

        /// <summary>
        /// Mock implementation of the <see cref="ITaskItem"/>.
        /// </summary>
        private class TaskItemMock : ITaskItem
        {
            public TaskItemMock(string assemblyName, string maxVersion)
            {
                ((ITaskItem)this).ItemSpec = assemblyName;
                MaxVersion = maxVersion;
            }

            public string MaxVersion { get; }

            string ITaskItem.ItemSpec { get; set; }

            ICollection ITaskItem.MetadataNames { get; }

            int ITaskItem.MetadataCount { get; }

            string ITaskItem.GetMetadata(string metadataName)
            {
                return MaxVersion;
            }

            void ITaskItem.SetMetadata(string metadataName, string metadataValue)
            {
                throw new NotImplementedException();
            }

            void ITaskItem.RemoveMetadata(string metadataName)
            {
                throw new NotImplementedException();
            }

            void ITaskItem.CopyMetadataTo(ITaskItem destinationItem)
            {
                throw new NotImplementedException();
            }

            IDictionary ITaskItem.CloneCustomMetadata()
            {
                throw new NotImplementedException();
            }
        }
    }
}
