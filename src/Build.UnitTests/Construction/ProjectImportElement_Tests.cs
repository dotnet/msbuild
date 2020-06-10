// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction
{
    public class ProjectImportElement_Tests
    {
        [Fact]
        public void SdkReferenceIsCorrect_CreatedFromOnDiskProject_SdkAndVersionAttributeSet()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFile projectFile = testEnvironment.CreateFile(
                    "test.proj",
                    @"
<Project>
  <Import Project=""Sdk.props"" Sdk=""My.Sdk"" Version=""1.2.0"" />
</Project>");
                ProjectRootElement rootElement = ProjectRootElement.Open(projectFile.Path);

                ProjectImportElement importElement = rootElement.Imports.First();

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Sdk = "My.Sdk");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Version = "1.2.0");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = "1.0.0", "Set Import Minimum Version 1.0.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.MinimumVersion = "1.0.0");

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("1.0.0");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Sdk = "Some.Other.Sdk", "Set Import Sdk Some.Other.Sdk");

                importElement.SdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("1.0.0");
            }
        }

        [Fact]
        public void SdkReferenceIsCorrect_CreatedFromOnDiskProject_SdkAttributeSet()
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestFile projectFile = testEnvironment.CreateFile(
                    "test.proj",
                    @"
<Project>
  <Import Project=""Sdk.props"" Sdk=""My.Sdk"" />
</Project>");
                ProjectRootElement rootElement = ProjectRootElement.Open(projectFile.Path);

                ProjectImportElement importElement = rootElement.Imports.First();

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBeNull();
                importElement.SdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Sdk = "My.Sdk");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Version = "1.2.0", "Set Import Version 1.2.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Version = "1.2.0");

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = "1.0.0", "Set Import Minimum Version 1.0.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.MinimumVersion = "1.0.0");

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("1.0.0");
            }
        }

        /// <summary>
        /// Verifies that the <see cref="ProjectImportElement.SdkReference" /> object is correctly set when creating <see cref="ProjectImportElement" /> objects.
        /// </summary>
        [Fact]
        public void SdkReferenceIsCorrect_CreatedInMemory()
        {
            using (var env = TestEnvironment.Create())
            {
                ProjectRootElement rootElement = ProjectRootElement.Create(NewProjectFileOptions.None);

                ProjectImportElement importElement = rootElement.AddImport("Sdk.props");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Sdk = "My.Sdk", "Set Import Sdk My.Sdk");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Sdk = "My.Sdk");

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBeNull();
                importElement.SdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Version = "1.2.0", "Set Import Version 1.2.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Version = "1.2.0");

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = "1.0.0", "Set Import Minimum Version 1.0.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.MinimumVersion = "1.0.0");

                importElement.SdkReference.Name.ShouldBe("My.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("1.0.0");

                rootElement.Save(env.GetTempFile(".csproj").Path);

                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Sdk = "My.Sdk");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Sdk = "Some.Other.Sdk", "Set Import Sdk Some.Other.Sdk");

                importElement.SdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.SdkReference.Version.ShouldBe("1.2.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("1.0.0");

                rootElement.Save();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Version = "4.0.0", "Set Import Version 4.0.0");

                importElement.SdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.SdkReference.Version.ShouldBe("4.0.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("1.0.0");

                rootElement.Save();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = "2.0.0", "Set Import Minimum Version 2.0.0");

                importElement.SdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.SdkReference.Version.ShouldBe("4.0.0");
                importElement.SdkReference.MinimumVersion.ShouldBe("2.0.0");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Version = null, "Set Import Version ");

                importElement.SdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.SdkReference.Version.ShouldBeNull();
                importElement.SdkReference.MinimumVersion.ShouldBe("2.0.0");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = null, "Set Import Minimum Version ");

                importElement.SdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.SdkReference.Version.ShouldBeNull();
                importElement.SdkReference.MinimumVersion.ShouldBeNull();
            }
        }

        private void SetPropertyAndExpectProjectXmlChangedEventToFire(ProjectRootElement rootElement, Action action, string expectedReason)
        {
            ProjectXmlChangedEventArgs projectXmlChangedEventArgs = null;

            void OnProjectXmlChanged(object sender, ProjectXmlChangedEventArgs args)
            {
                projectXmlChangedEventArgs = args;
            }

            rootElement.OnProjectXmlChanged += OnProjectXmlChanged;
            try
            {
                action();
            }
            finally
            {
                rootElement.OnProjectXmlChanged -= OnProjectXmlChanged;
            }

            projectXmlChangedEventArgs.ShouldNotBeNull();
            projectXmlChangedEventArgs.Reason.ShouldBe(expectedReason);
        }

        private void SetPropertyAndExpectProjectXmlChangedEventToNotFire(ProjectRootElement rootElement, Action action)
        {
            ProjectXmlChangedEventArgs projectXmlChangedEventArgs = null;

            void OnProjectXmlChanged(object sender, ProjectXmlChangedEventArgs args)
            {
                projectXmlChangedEventArgs = args;
            }

            rootElement.OnProjectXmlChanged += OnProjectXmlChanged;
            try
            {
                action();
            }
            finally
            {
                rootElement.OnProjectXmlChanged -= OnProjectXmlChanged;
            }

            projectXmlChangedEventArgs.ShouldBeNull();
        }
    }
}
