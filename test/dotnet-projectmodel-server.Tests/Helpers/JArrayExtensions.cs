// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public static class JArrayExtensions
    {
        public static JArray AssertJArrayEmpty(this JArray array)
        {
            Assert.NotNull(array);
            Assert.Empty(array);

            return array;
        }

        public static JArray AssertJArrayNotEmpty(this JArray array)
        {
            Assert.NotNull(array);
            Assert.NotEmpty(array);

            return array;
        }

        public static JArray AssertJArrayCount(this JArray array, int expectedCount)
        {
            Assert.NotNull(array);
            Assert.Equal(expectedCount, array.Count);

            return array;
        }

        public static JArray AssertJArrayElement<T>(this JArray array, int index, T expectedElementValue)
        {
            Assert.NotNull(array);

            var element = array[index];
            Assert.NotNull(element);
            Assert.Equal(expectedElementValue, element.Value<T>());

            return array;
        }

        public static JArray AssertJArrayContains<T>(this JArray array, T value)
        {
            AssertJArrayContains<T>(array, element => object.Equals(element, value));

            return array;
        }

        public static JArray AssertJArrayContains<T>(this JArray array, Func<T, bool> critiera)
        {
            bool contains = false;
            foreach (var element in array)
            {
                var value = element.Value<T>();

                contains = critiera(value);
                if (contains)
                {
                    break;
                }
            }

            Assert.True(contains, "JArray doesn't contains the specified element.");

            return array;
        }

        public static JArray AssertJArrayNotContains<T>(this JArray array, Func<T, bool> critiera)
        {
            foreach (var element in array)
            {
                var value = element.Value<T>();

                if (critiera(value))
                {
                    Assert.True(false, "JArray contains unexpected element.");
                }
            }

            return array;
        }

        public static T RetrieveArraryElementAs<T>(this JArray json, int index)
            where T : JToken
        {
            Assert.NotNull(json);
            Assert.True(index >= 0 && index < json.Count, "Index out of range");

            var element = json[index];
            DthMessageExtension.AssertType<T>(element, $"Element at {index}");

            return (T)element;
        }
    }
}
