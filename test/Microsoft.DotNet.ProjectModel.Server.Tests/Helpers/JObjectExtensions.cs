// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public static class JObjectExtensions
    {
        public static JObject AsJObject(this JToken token)
        {
            DthMessageExtension.AssertType<JObject>(token, nameof(JToken));

            return (JObject)token;
        }

        public static JObject RetrieveDependencyDiagnosticsErrorAt(this JObject payload, int index)
        {
            Assert.NotNull(payload);

            return payload.RetrievePropertyAs<JArray>("Errors")
                          .RetrieveArraryElementAs<JObject>(index);
        }

        public static T RetrieveDependencyDiagnosticsErrorAt<T>(this JObject payload, int index)
            where T : JToken
        {
            Assert.NotNull(payload);

            return payload.RetrievePropertyAs<JArray>("Errors")
                          .RetrieveArraryElementAs<T>(index);
        }

        public static T RetrievePropertyAs<T>(this JObject json, string propertyName)
            where T : JToken
        {
            Assert.NotNull(json);

            var property = json[propertyName];
            Assert.NotNull(property);
            DthMessageExtension.AssertType<T>(property, $"Property {propertyName}");

            return (T)property;
        }

        public static JObject AssertProperty<T>(this JObject json, string propertyName, T expectedPropertyValue)
        {
            Assert.NotNull(json);

            var property = json[propertyName];
            Assert.NotNull(property);
            Assert.Equal(expectedPropertyValue, property.Value<T>());

            return json;
        }

        public static JObject AssertProperty<T>(this JObject json, string propertyName, Func<T, bool> assertion)
        {
            return AssertProperty<T>(json,
                                     propertyName,
                                     assertion,
                                     value => $"Assert failed on {propertyName}.");
        }

        public static JObject AssertProperty<T>(this JObject json, string propertyName, Func<T, bool> assertion, Func<T, string> errorMessage)
        {
            Assert.NotNull(json);

            var property = json[propertyName];
            Assert.False(property == null, $"Property {propertyName} doesn't exist.");

            var propertyValue = property.Value<T>();
            Assert.False(propertyValue == null, $"Property {propertyName} of type {typeof(T).Name} doesn't exist.");

            Assert.True(assertion(propertyValue),
                        errorMessage(propertyValue));

            return json;
        }
    }
}
