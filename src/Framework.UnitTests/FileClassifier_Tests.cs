// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class FileClassifierTests
    {
        [Fact]
        public void Shared_ReturnsInstance()
        {
            FileClassifier.Shared.ShouldNotBeNull();
        }

        [Fact]
        public void IsNonModifiable_EvaluatesModifiability()
        {
            FileClassifier classifier = new FileClassifier();

            classifier.RegisterNuGetPackageFolders("X:\\Test1;X:\\Test2");

            classifier.IsNonModifiable("X:\\Test1\\File.ext").ShouldBeTrue();
            classifier.IsNonModifiable("X:\\Test2\\File.ext").ShouldBeTrue();
            classifier.IsNonModifiable("X:\\Test3\\File.ext").ShouldBeFalse();
        }

        [Fact]
        public void IsNonModifiable_DoesntThrowWhenPackageFoldersAreNotRegistered()
        {
            FileClassifier classifier = new FileClassifier();

            classifier.IsNonModifiable("X:\\Test3\\File.ext").ShouldBeFalse();
        }
    }
}
