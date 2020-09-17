using MessagePack;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Client;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;
using Microsoft.Build.Utilities;
using Nerdbank.Streams;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

        [Fact]
        public void EnsureInputPropertiesMatch()
        {
            string[] rarInputProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => !p.GetCustomAttributes(typeof(OutputAttribute), inherit: true).Any()).Select(p => p.Name).ToArray();
            string[] inputProperties = typeof(ResolveAssemblyReferenceRequest).GetProperties().Select(p => p.Name).ToArray();

            foreach (var item in rarInputProperties)
            {
                inputProperties.ShouldContain(item);
            }
        }

        [Fact]
        public void EnsureOutputPropertiesMatch()
        {
            string[] rarInputProperties = typeof(ResolveAssemblyReference).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.GetCustomAttributes(typeof(OutputAttribute), true).Any()).Select(p => p.Name).ToArray();
            string[] inputProperties = typeof(ResolveAssemblyReferenceResponse).GetProperties().Select(p => p.Name).ToArray();

            foreach (var item in rarInputProperties)
            {
                inputProperties.ShouldContain(item);
            }
        }

        [Fact]
        public void TransferredRequestEquals()
        {
            MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithResolver(ResolveAssemblyReferneceResolver.Instance);
            ResolveAssemblyReferenceRequest request = GetPopulatedObject<ResolveAssemblyReferenceRequest>("test", new[] { "testArr" }, true, new[] { new ReadOnlyTaskItem("test") });

            byte[] data = MessagePackSerializer.Serialize(request, options);
            ResolveAssemblyReferenceRequest requestDes = MessagePackSerializer.Deserialize<ResolveAssemblyReferenceRequest>(data, options);

            ResolveAssemblyReferenceComparer.CompareInput(request, requestDes).ShouldBeTrue();
        }

        [Fact]
        public void TransferredResponseEquals()
        {
            MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithResolver(ResolveAssemblyReferneceResolver.Instance);
            ResolveAssemblyReferenceResponse response = GetPopulatedObject<ResolveAssemblyReferenceResponse>("test", new[] { "testArr" }, true, new[] { new ReadOnlyTaskItem("test") });

            byte[] data = MessagePackSerializer.Serialize(response, options);
            ResolveAssemblyReferenceResponse responseDes = MessagePackSerializer.Deserialize<ResolveAssemblyReferenceResponse>(data, options);

            ResolveAssemblyReferenceComparer.CompareOutput(response, responseDes).ShouldBeTrue();
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

            ResolveAssemblyReferenceComparer.CompareOutput(expectedResult.Response, result.Response).ShouldBeTrue();

            serverStream.Dispose();
            clientStream.Dispose();
        }

        private T GetPopulatedObject<T>(string str, string[] strArray, bool boolVal, ReadOnlyTaskItem[] taskItems) where T : new()
        {
            int count = 0;
            T request = new T();
            Type t = typeof(T);
            t.GetConstructor(Type.EmptyTypes).Invoke(null);
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var propType = prop.PropertyType;
                if (propType == typeof(string))
                {
                    prop.SetValue(request, str + count++);
                }
                else if (propType == typeof(string[]))
                {
                    if (strArray?.Length > 0)
                    {
                        strArray[0] += count++;
                    }

                    prop.SetValue(request, strArray);
                }
                else if (propType == typeof(bool))
                {
                    prop.SetValue(request, boolVal);
                }
                else if (propType == typeof(ReadOnlyTaskItem[]))
                {
                    if (taskItems?.Length > 0 && taskItems[0] != null)
                    {
                        taskItems[0].ItemSpec += count++;
                    }

                    prop.SetValue(request, taskItems);
                }
                else
                {
                    // Fix by adding new if with this type
                    throw new NotImplementedException($"Invalid type: {propType.FullName}");
                }
            }
            return request;
        }
    }
}
