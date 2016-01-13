// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public static class DthMessageExtension
    {
        public static JObject RetrieveDependency(this DthMessage message, string dependencyName)
        {
            Assert.NotNull(message);
            Assert.Equal(MessageTypes.Dependencies, message.MessageType);

            var payload = message.Payload as JObject;
            Assert.NotNull(payload);

            var dependency = payload[MessageTypes.Dependencies][dependencyName] as JObject;
            Assert.NotNull(dependency);
            Assert.Equal(dependencyName, dependency["Name"].Value<string>());

            return dependency;
        }

        public static DthMessage EnsureNotContainDependency(this DthMessage message, string dependencyName)
        {
            Assert.NotNull(message);
            Assert.Equal(MessageTypes.Dependencies, message.MessageType);

            var payload = message.Payload as JObject;
            Assert.NotNull(payload);

            Assert.True(payload[MessageTypes.Dependencies][dependencyName] == null, $"Unexpected dependency {dependencyName} exists.");

            return message;
        }

        public static JObject RetrieveDependencyDiagnosticsCollection(this DthMessage message)
        {
            Assert.NotNull(message);
            Assert.Equal(MessageTypes.DependencyDiagnostics, message.MessageType);

            var payload = message.Payload as JObject;
            Assert.NotNull(payload);

            return payload;
        }

        public static T RetrievePayloadAs<T>(this DthMessage message)
            where T : JToken
        {
            Assert.NotNull(message);
            AssertType<T>(message.Payload, "Payload");

            return (T)message.Payload;
        }

        /// <summary>
        /// Throws if the message is not generated in communication between given server and client
        /// </summary>
        public static DthMessage EnsureSource(this DthMessage message, DthTestServer server, DthTestClient client)
        {
            if (message.HostId != server.HostId)
            {
                throw new Exception($"{nameof(message.HostId)} doesn't match the one of server. Expected {server.HostId} but actually {message.HostId}.");
            }

            return message;
        }

        public static void AssertType<T>(object obj, string name)
        {
            Assert.True(obj is T, $"{name} is not of type {typeof(T).Name}.");
        }
    }
}
