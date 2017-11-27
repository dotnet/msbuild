// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using System.IO;
using Microsoft.DotNet.Configurer;
using System.Linq;

namespace Microsoft.DotNet.Cli.Telemetry
{
    internal class TelemetryCommonProperties
    {
        public TelemetryCommonProperties(
            Func<string> getCurrentDirectory = null,
            Func<string, string> hasher = null,
            Func<string> getMACAddress = null,
            IDockerContainerDetector dockerContainerDetector = null,
            IUserLevelCacheWriter userLevelCacheWriter = null)
        {
            _getCurrentDirectory = getCurrentDirectory ?? Directory.GetCurrentDirectory;
            _hasher = hasher ?? Sha256Hasher.Hash;
            _getMACAddress = getMACAddress ?? MacAddressGetter.GetMacAddress;
            _dockerContainerDetector = dockerContainerDetector ?? new DockerContainerDetectorForTelemetry();
            _userLevelCacheWriter = userLevelCacheWriter ?? new UserLevelCacheWriter(new CliFolderPathCalculator());
        }

        private readonly IDockerContainerDetector _dockerContainerDetector;
        private Func<string> _getCurrentDirectory;
        private Func<string, string> _hasher;
        private Func<string> _getMACAddress;
        private IUserLevelCacheWriter _userLevelCacheWriter;
        private const string OSVersion = "OS Version";
        private const string OSPlatform = "OS Platform";
        private const string RuntimeId = "Runtime Id";
        private const string ProductVersion = "Product Version";
        private const string TelemetryProfile = "Telemetry Profile";
        private const string CurrentPathHash = "Current Path Hash";
        private const string MachineId = "Machine ID";
        private const string DockerContainer = "Docker Container";
        private const string TelemetryProfileEnvironmentVariable = "DOTNET_CLI_TELEMETRY_PROFILE";
        private const string CannotFindMacAddress = "Unknown";

        private const string MachineIdCacheKey = "MachineId";
        private const string IsDockerContainerCacheKey = "IsDockerContainer";

        public Dictionary<string, string> GetTelemetryCommonProperties()
        {
            return new Dictionary<string, string>
            {
                {OSVersion, RuntimeEnvironment.OperatingSystemVersion},
                {OSPlatform, RuntimeEnvironment.OperatingSystemPlatform.ToString()},
                {RuntimeId, RuntimeEnvironment.GetRuntimeIdentifier()},
                {ProductVersion, Product.Version},
                {TelemetryProfile, Environment.GetEnvironmentVariable(TelemetryProfileEnvironmentVariable)},
                {DockerContainer, _userLevelCacheWriter.RunWithCache(IsDockerContainerCacheKey, () => _dockerContainerDetector.IsDockerContainer().ToString("G") )},
                {CurrentPathHash, _hasher(_getCurrentDirectory())},
                {MachineId, _userLevelCacheWriter.RunWithCache(MachineIdCacheKey, GetMachineId)}
            };
        }

        private string GetMachineId()
        {
            var macAddress = _getMACAddress();
            if (macAddress != null)
            {
                return _hasher(macAddress);
            }
            else
            {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
