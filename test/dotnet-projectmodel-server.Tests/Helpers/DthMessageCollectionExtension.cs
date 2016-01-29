// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public static class DthMessageCollectionExtension
    {
        public static IList<DthMessage> GetMessagesByFramework(this IEnumerable<DthMessage> messages, FrameworkName targetFramework)
        {
            return messages.Where(msg => MatchesFramework(targetFramework, msg)).ToList();
        }

        public static IList<DthMessage> GetMessagesByType(this IEnumerable<DthMessage> messages, string typename)
        {
            return messages.Where(msg => string.Equals(msg.MessageType, typename)).ToList();
        }

        public static DthMessage RetrieveSingleMessage(this IEnumerable<DthMessage> messages,
                                                       string typename)
        {
            var result = messages.SingleOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal));

            if (result == null)
            {
                if (messages.FirstOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal)) != null)
                {
                    Assert.False(true, $"More than one {typename} messages exist.");
                }
                else
                {
                    Assert.False(true, $"{typename} message doesn't exists.");
                }
            }

            return result;
        }

        public static IEnumerable<DthMessage> ContainsMessage(this IEnumerable<DthMessage> messages,
                                                              string typename)
        {
            var contain = messages.FirstOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal)) != null;

            Assert.True(contain, $"Messages collection doesn't contain message of type {typename}.");

            return messages;
        }

        public static IEnumerable<DthMessage> AssertDoesNotContain(this IEnumerable<DthMessage> messages, string typename)
        {
            var notContain = messages.FirstOrDefault(msg => string.Equals(msg.MessageType, typename, StringComparison.Ordinal)) == null;

            Assert.True(notContain, $"Message collection contains message of type {typename}.");

            return messages;
        }

        private static bool MatchesFramework(FrameworkName targetFramework, DthMessage msg)
        {
            if (msg.Payload.Type != JTokenType.Object)
            {
                return false;
            }

            var frameworkObj = msg.Payload["Framework"];

            if (frameworkObj == null || !frameworkObj.HasValues)
            {
                return false;
            }

            return string.Equals(frameworkObj.Value<string>("FrameworkName"), targetFramework.FullName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
