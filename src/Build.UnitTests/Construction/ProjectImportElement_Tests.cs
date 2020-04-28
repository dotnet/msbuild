// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Shouldly;
using System;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction
{
    public class ProjectImportElement_Tests
    {
        /// <summary>
        /// Verifies that the <see cref="ProjectImportElement.ParsedSdkReference" /> object is correctly set when creating <see cref="ProjectImportElement" /> objects.
        /// </summary>
        [Fact]
        public void SdkReferenceIsCorrect()
        {
            using (var env = TestEnvironment.Create())
            {
                ProjectRootElement rootElement = ProjectRootElement.Create(NewProjectFileOptions.None);

                ProjectImportElement importElement = rootElement.AddImport("Sdk.props");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Sdk = "My.Sdk", "Set Import Sdk My.Sdk");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Sdk = "My.Sdk");

                importElement.ParsedSdkReference.Name.ShouldBe("My.Sdk");
                importElement.ParsedSdkReference.Version.ShouldBeNull();
                importElement.ParsedSdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Version = "1.2.0", "Set Import Version 1.2.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Version = "1.2.0");

                importElement.ParsedSdkReference.Name.ShouldBe("My.Sdk");
                importElement.ParsedSdkReference.Version.ShouldBe("1.2.0");
                importElement.ParsedSdkReference.MinimumVersion.ShouldBeNull();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = "1.0.0", "Set Import Minimum Version 1.0.0");
                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.MinimumVersion = "1.0.0");

                importElement.ParsedSdkReference.Name.ShouldBe("My.Sdk");
                importElement.ParsedSdkReference.Version.ShouldBe("1.2.0");
                importElement.ParsedSdkReference.MinimumVersion.ShouldBe("1.0.0");

                rootElement.Save(env.GetTempFile(".csproj").Path);

                SetPropertyAndExpectProjectXmlChangedEventToNotFire(rootElement, () => importElement.Sdk = "My.Sdk");

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Sdk = "Some.Other.Sdk", "Set Import Sdk Some.Other.Sdk");

                importElement.ParsedSdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.ParsedSdkReference.Version.ShouldBe("1.2.0");
                importElement.ParsedSdkReference.MinimumVersion.ShouldBe("1.0.0");

                rootElement.Save();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.Version = "4.0.0", "Set Import Version 4.0.0");

                importElement.ParsedSdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.ParsedSdkReference.Version.ShouldBe("4.0.0");
                importElement.ParsedSdkReference.MinimumVersion.ShouldBe("1.0.0");

                rootElement.Save();

                SetPropertyAndExpectProjectXmlChangedEventToFire(rootElement, () => importElement.MinimumVersion = "2.0.0", "Set Import Minimum Version 2.0.0");

                importElement.ParsedSdkReference.Name.ShouldBe("Some.Other.Sdk");
                importElement.ParsedSdkReference.Version.ShouldBe("4.0.0");
                importElement.ParsedSdkReference.MinimumVersion.ShouldBe("2.0.0");
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
