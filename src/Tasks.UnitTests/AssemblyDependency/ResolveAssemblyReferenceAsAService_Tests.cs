// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MessagePack;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Client;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;
using Microsoft.Build.Utilities;
using Nerdbank.Streams;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    public sealed class ResolveAssemblyReferenceAsAService_Tests : ResolveAssemblyReferenceTestFixture
    {
        public ResolveAssemblyReferenceAsAService_Tests(ITestOutputHelper output) : base(output)
        {
        }

        [InlineData(typeof(ResolveAssemblyReferenceRequest), false)]
        [InlineData(typeof(ResolveAssemblyReferenceResponse), true)]
        [Theory]
        public void EnsurePropertiesMatch(Type t, bool isOutputProperty)
        {
            string[] rarProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => isOutputProperty == p.GetCustomAttributes(typeof(OutputAttribute), inherit: true).Any()).Select(p => p.Name).ToArray();
            string[] properties = t.GetProperties().Select(p => p.Name).ToArray();

            foreach (var item in rarProperties)
            {
                properties.ShouldContain(item);
            }
        }

        [InlineData(typeof(ResolveAssemblyReferenceRequest), RequestFormatter.MemberCount)]
        [InlineData(typeof(ResolveAssemblyReferenceResponse), ResponseFormatter.MemberCount)]
        [InlineData(typeof(ResolveAssemblyReferenceResult), ResultFormatter.MemberCount)]
        [Theory]
        public void FormatterHeaderSizeMatchTest(Type type, int memberCount)
        {
            int propertyCount = type.GetProperties().Length;

            propertyCount.ShouldBe(memberCount);
        }

        [InlineData(typeof(ResolveAssemblyReferenceRequest))]
        [InlineData(typeof(ResolveAssemblyReferenceResponse))]
        [Theory]
        public void TransferredObjectsEqual(Type type)
        {
            MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithResolver(ResolveAssemblyReferneceResolver.Instance);
            object obj = GetPopulatedObject(type, "test", new[] { "testArr" }, true, new[] { new TaskItem("test") });

            byte[] data = MessagePackSerializer.Serialize(type, obj, options);
            object objDes = MessagePackSerializer.Deserialize(type, data, options);

            objDes.ShouldBeOfType(type);

            if (typeof(ResolveAssemblyReferenceRequest).Equals(type))
            {
                ResolveAssemblyReferenceComparer.CompareRequest((ResolveAssemblyReferenceRequest)obj, (ResolveAssemblyReferenceRequest)objDes).ShouldBeTrue();
            }
            else if (typeof(ResolveAssemblyReferenceResponse).Equals(type))
            {
                ResolveAssemblyReferenceComparer.CompareResponse((ResolveAssemblyReferenceResponse)obj, (ResolveAssemblyReferenceResponse)objDes).ShouldBeTrue();
            }
            else
            {
                objDes.ShouldBe(obj);
            }
        }

        [Fact]
        public void RarOutputPropertyTest()
        {
            ResolveAssemblyReferenceResponse expectedResponse = GetPopulatedObject<ResolveAssemblyReferenceResponse>("test", new[] { "testArr" }, true, new[] { new TaskItem("test") });

            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.ResolveAssemblyReferenceOutput = expectedResponse;
            ResolveAssemblyReferenceResponse response = rar.ResolveAssemblyReferenceOutput;

            ResolveAssemblyReferenceComparer.CompareResponse(expectedResponse, response).ShouldBeTrue();
        }

        [Fact]
        public void RarIputPropertyTest()
        {
            ResolveAssemblyReferenceRequest expectedRequest = GetPopulatedObject<ResolveAssemblyReferenceRequest>("test", new[] { "testArr" }, true, new[] { new TaskItem("test") });
            expectedRequest.CurrentPath = Directory.GetCurrentDirectory();
            expectedRequest.WarnOrErrorOnTargetArchitectureMismatch = "None"; // Serialized into enum, so we have to provide correct value

            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.ResolveAssemblyReferenceInput = expectedRequest;
            ResolveAssemblyReferenceRequest request = rar.ResolveAssemblyReferenceInput;

            ResolveAssemblyReferenceComparer.CompareRequest(expectedRequest, request).ShouldBeTrue();
        }


        [Fact]
        public void TransmitDataTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            (Stream serverStream, Stream clientStream) = FullDuplexStream.CreatePair();
            MockEngine e = new MockEngine(_output)
            {
                ClientStream = clientStream
            };

            RarController controller = new RarController(string.Empty, null, null);
            Task serverTask = controller.HandleClientAsync(serverStream, cts.Token);
            RarClient client = new RarClient(e);
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            ResolveAssemblyReference rar = new ResolveAssemblyReference
            {
                Assemblies = assemblyNames
            };

            ResolveAssemblyReferenceHandler handler = new ResolveAssemblyReferenceHandler();
            ResolveAssemblyReferenceResult expectedResult = handler.Execute(rar.ResolveAssemblyReferenceInput);

            client.Connect();
            ResolveAssemblyReferenceResult result = client.Execute(rar.ResolveAssemblyReferenceInput);
            cts.Cancel();

            ResolveAssemblyReferenceComparer.CompareResponse(expectedResult.Response, result.Response).ShouldBeTrue();

            serverStream.Dispose();
            clientStream.Dispose();
        }

        private T GetPopulatedObject<T>(string str, string[] strArray, bool boolVal, ITaskItem[] taskItems) where T : new()
        {
            return (T)GetPopulatedObject(typeof(T), str, strArray, boolVal, taskItems);
        }

        private object GetPopulatedObject(Type type, string str, string[] strArray, bool boolVal, ITaskItem[] taskItems)
        {
            int count = 0;
            object obj = Activator.CreateInstance(type);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var propType = prop.PropertyType;
                if (propType == typeof(string))
                {
                    prop.SetValue(obj, str + count++);
                }
                else if (propType == typeof(string[]))
                {
                    if (strArray?.Length > 0)
                    {
                        strArray[0] += count++;
                    }

                    prop.SetValue(obj, strArray);
                }
                else if (propType == typeof(bool))
                {
                    prop.SetValue(obj, boolVal);
                }
                else if (propType == typeof(ITaskItem[]))
                {
                    if (taskItems?.Length > 0 && taskItems[0] != null)
                    {
                        taskItems[0].ItemSpec += count++;
                    }

                    prop.SetValue(obj, taskItems);
                }
                else
                {
                    // Fix by adding new if with this type
                    throw new NotImplementedException($"Invalid type: {propType.FullName}");
                }
            }
            return obj;
        }
    }
}
