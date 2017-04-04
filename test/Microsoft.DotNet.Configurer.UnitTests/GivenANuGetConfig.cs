// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NuGet.Configuration;
using Xunit;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenANuGetConfig
    {
        private const string PathToFallbackFolderAlreadySet = "some path to fallback folder";

        private Mock<ISettings> _settingsMock;
        private INuGetConfig _nugetConfig;

        public GivenANuGetConfig()
        {
            _settingsMock = new Mock<ISettings>();
            _settingsMock
                .Setup(s => s.GetSettingValues(NuGetConfig.FallbackPackageFolders, false))
                .Returns(new List<SettingValue>()
                {
                    new SettingValue("CliFallbackFolder", PathToFallbackFolderAlreadySet, false)
                });

            _nugetConfig = new NuGetConfig(_settingsMock.Object);
        }

        [Fact]
        public void ItAddsACliFallbackFolderIfOneIsNotPresentAlready()
        {
            const string FallbackFolderNotAlreadySet = "some path not already set";
            _nugetConfig.AddCliFallbackFolder(FallbackFolderNotAlreadySet);

            _settingsMock.Verify(s =>
                s.SetValue(NuGetConfig.FallbackPackageFolders, "CliFallbackFolder", FallbackFolderNotAlreadySet),
                Times.Exactly(1));
        }

        [Fact]
        public void ItDoesNotAddTheCliFallbackFolderIfItIsAlreadyPresent()
        {
            _nugetConfig.AddCliFallbackFolder(PathToFallbackFolderAlreadySet);

            _settingsMock.Verify(s => 
                s.SetValue(NuGetConfig.FallbackPackageFolders, "CliFallbackFolder", PathToFallbackFolderAlreadySet),
                Times.Never);
        }
    }
}