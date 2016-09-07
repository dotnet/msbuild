// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel.Server.Models;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProtocolManager
    {
        /// <summary>
        /// Environment variable for overriding protocol.
        /// </summary>
        public const string EnvDthProtocol = "DTH_PROTOCOL";

        public ProtocolManager(int maxVersion)
        {
            MaxVersion = maxVersion;

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

            Reporter.Output.WriteLine("Initializing the protocol negotiation.");

            if (EnvironmentOverridden)
            {
                Reporter.Output.WriteLine($"DTH protocol negotiation is override by environment variable {EnvDthProtocol} and set to {CurrentVersion}.");
                return;
            }

            var tokenValue = message.Payload?["Version"];
            if (tokenValue == null)
            {
                Reporter.Output.WriteLine("Protocol negotiation failed. Version property is missing in payload.");
                return;
            }

            var preferredVersion = tokenValue.ToObject<int>();
            if (preferredVersion == 0)
            {
                // the preferred version can't be zero. either property is missing or the the payload is corrupted.
                Reporter.Output.WriteLine("Protocol negotiation failed. Protocol version 0 is invalid.");
                return;
            }

            CurrentVersion = Math.Min(preferredVersion, MaxVersion);
            Reporter.Output.WriteLine($"Protocol negotiation successed. Use protocol {CurrentVersion}");

            if (message.Sender != null)
            {
                Reporter.Output.WriteLine("Respond to protocol negotiation.");
                message.Sender.Transmit(Message.FromPayload(
                    MessageTypes.ProtocolVersion,
                    0,
                    new { Version = CurrentVersion }));
            }
            else
            {
                Reporter.Output.WriteLine($"{nameof(Message.Sender)} is null.");
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
