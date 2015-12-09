// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.Server.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProtocolManager
    {
        /// <summary>
        /// Environment variable for overriding protocol.
        /// </summary>
        public const string EnvDthProtocol = "DTH_PROTOCOL";

        private readonly ILogger _log;

        public ProtocolManager(int maxVersion, ILoggerFactory loggerFactory)
        {
            MaxVersion = maxVersion;
            _log = loggerFactory.CreateLogger<ProtocolManager>();

            // initialized to the highest supported version or environment overridden value
            int? protocol = GetProtocolVersionFromEnvironment();

            if (protocol.HasValue)
            {
                CurrentVersion = protocol.Value;
                EnvironmentOverridden = true;
            }
            else
            {
                CurrentVersion = 4;
            }
        }

        public int MaxVersion { get; }

        public int CurrentVersion { get; private set; }

        public bool EnvironmentOverridden { get; }

        public bool IsProtocolNegotiation(Message message)
        {
            return message?.MessageType == MessageTypes.ProtocolVersion;
        }

        public void Negotiate(Message message)
        {
            if (!IsProtocolNegotiation(message))
            {
                return;
            }

            _log.LogInformation("Initializing the protocol negotiation.");

            if (EnvironmentOverridden)
            {
                _log.LogInformation($"DTH protocol negotiation is override by environment variable {EnvDthProtocol} and set to {CurrentVersion}.");
                return;
            }

            var tokenValue = message.Payload?["Version"];
            if (tokenValue == null)
            {
                _log.LogInformation("Protocol negotiation failed. Version property is missing in payload.");
                return;
            }

            var preferredVersion = tokenValue.ToObject<int>();
            if (preferredVersion == 0)
            {
                // the preferred version can't be zero. either property is missing or the the payload is corrupted.
                _log.LogInformation("Protocol negotiation failed. Protocol version 0 is invalid.");
                return;
            }

            CurrentVersion = Math.Min(preferredVersion, MaxVersion);
            _log.LogInformation($"Protocol negotiation successed. Use protocol {CurrentVersion}");

            if (message.Sender != null)
            {
                _log.LogInformation("Respond to protocol negotiation.");
                message.Sender.Transmit(Message.FromPayload(
                    MessageTypes.ProtocolVersion,
                    0,
                    new { Version = CurrentVersion }));
            }
            else
            {
                _log.LogInformation($"{nameof(Message.Sender)} is null.");
            }
        }

        private static int? GetProtocolVersionFromEnvironment()
        {
            // look for the environment variable DTH_PROTOCOL, if it is set override the protocol version.
            // this is for debugging.
            var strProtocol = Environment.GetEnvironmentVariable(EnvDthProtocol);
            int intProtocol = -1;
            if (!string.IsNullOrEmpty(strProtocol) && Int32.TryParse(strProtocol, out intProtocol))
            {
                return intProtocol;
            }

            return null;
        }
    }
}
