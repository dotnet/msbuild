// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;

using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Collections;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class TargetInProgressState_Tests
    {

        [Test]
        public void TargetInProgressStateCustomSerialization()
        {
            Engine engine = new Engine(@"c:\");
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"
                   <Project DefaultTargets=`t` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                        <OutputPath>bin\Debug\</OutputPath>
                        <AssemblyName>MyAssembly</AssemblyName>
                        <OutputType>Exe</OutputType>
                        <Configuration>Debug</Configuration>
                      </PropertyGroup>
                      <ItemGroup>
                        <Compile Include=`Class1.cs` />
                        <EmbeddedResource Include=`Resource1.txt` />
                        <EmbeddedResource Include=`Resource2.resx` />
                      </ItemGroup>
                      <Target Name='t' DependsOnTargets='Build'/>
                    <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.Targets` />
                    </Project>
                "); 
            EngineCallback engineCallback = new EngineCallback(engine);
            Target build = project.Targets["Build"];
            List<ProjectBuildState> waitingBuildStates = null;

            int handleId = 1;
            string projectFileName = "ProjectFileName";
            string[] targetNames = new string[] { "t" };
            Dictionary<string, string> dictionary = null;
            int requestId = 1;
            BuildRequest request = new BuildRequest(handleId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            ArrayList targetNamesToBuild = new ArrayList();
            targetNamesToBuild.Add("t");
            ProjectBuildState initiatingRequest = new ProjectBuildState(request, targetNamesToBuild, new BuildEventContext(1, 2, 2, 2));
            initiatingRequest.AddBlockingTarget("Build");
            BuildRequest [] outstandingBuildRequests = null;
            string projectName = "SuperTestProject";
            
            

            TargetInProgessState targetInProgress1 = new TargetInProgessState(
                                                              engineCallback,
                                                              build,
                                                              waitingBuildStates,
                                                              initiatingRequest,
                                                              outstandingBuildRequests,
                                                              projectName
                                                          );
            
            targetInProgress1.ParentTargetsForBuildRequests = null;
            Assertion.AssertNull(targetInProgress1.ParentTargetsForBuildRequests);
            Assertion.Assert(!targetInProgress1.RequestedByHost);

            build = project.Targets["t"];
             request = new BuildRequest(handleId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            request.IsExternalRequest = true;
            targetNamesToBuild.Add("t");
            initiatingRequest = new ProjectBuildState(request, targetNamesToBuild, new BuildEventContext(1, 2, 2, 2));
            outstandingBuildRequests = new BuildRequest[]{
                new BuildRequest(1, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false),
                new BuildRequest(2, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false),
                new BuildRequest(3, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false),
                new BuildRequest(4, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false)
            };

            TargetInProgessState.TargetIdWrapper originalWrapper = new TargetInProgessState.TargetIdWrapper();
            originalWrapper.id = 1;
            originalWrapper.name = "Wrapper";
            originalWrapper.nodeId = 4;
            originalWrapper.projectId = 6;
            waitingBuildStates = new List<ProjectBuildState>();
            waitingBuildStates.Add(initiatingRequest);

            TargetInProgessState targetInProgress3 = new TargetInProgessState(
                                                              engineCallback,
                                                              build,
                                                              waitingBuildStates,
                                                              initiatingRequest,
                                                              outstandingBuildRequests,
                                                              projectName
                                                          );
            targetInProgress3.ParentTargetsForBuildRequests = new TargetInProgessState.TargetIdWrapper[] { originalWrapper };

            // Stream, writer and reader where the events will be serialized and deserialized from
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                stream.Position = 0;
                // Serialize
                targetInProgress3.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                long streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                TargetInProgessState newInProgressState = new TargetInProgessState();
                newInProgressState.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(string.Compare(newInProgressState.ProjectName, projectName, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(newInProgressState.TargetId.name, "t", StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsNotNull(newInProgressState.ParentTargets);
                Assert.IsTrue(newInProgressState.OutstandingBuildRequests.Length == 4);
                Assert.IsNotNull(newInProgressState.ParentBuildRequests);
                Assert.IsNotNull(newInProgressState.ParentTargetsForBuildRequests.Length == 1);

                stream.Position = 0;
                // Serialize
                targetInProgress1.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                 streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                newInProgressState = new TargetInProgessState();
                newInProgressState.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(string.Compare(newInProgressState.ProjectName, projectName,StringComparison.OrdinalIgnoreCase)==0);
                Assert.IsTrue(string.Compare(newInProgressState.TargetId.name,"Build",StringComparison.OrdinalIgnoreCase)==0);
                Assert.IsNotNull(newInProgressState.ParentTargets);
                Assert.IsNull(newInProgressState.OutstandingBuildRequests);
                Assert.IsNotNull(newInProgressState.ParentBuildRequests);

                TargetInProgessState targetInProgress2 = new TargetInProgessState();
                stream.Position = 0;
                // Serialize
                targetInProgress2.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                newInProgressState = new TargetInProgessState();
                newInProgressState.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsNull(newInProgressState.ProjectName);
                Assert.IsNull(newInProgressState.TargetId);
                Assert.IsNull(newInProgressState.ParentTargets);
                Assert.IsNull(newInProgressState.OutstandingBuildRequests);
                Assert.IsNull(newInProgressState.ParentBuildRequests);
            }
            finally
            {
                // Close will close the writer/reader and the underlying stream
                writer.Close();
                reader.Close();
                reader = null;
                stream = null;
                writer = null;
            }
        }


        [Test]
        public void TargetIdWrapperCustomSerialization()
        {
            // Stream, writer and reader where the events will be serialized and deserialized from
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                TargetInProgessState.TargetIdWrapper originalWrapper = new TargetInProgessState.TargetIdWrapper();
                originalWrapper.id = 1;
                originalWrapper.name = "Wrapper";
                originalWrapper.nodeId = 4;
                originalWrapper.projectId = 6;

                stream.Position = 0;
                // Serialize
                originalWrapper.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                long streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                TargetInProgessState.TargetIdWrapper newWrapper = new TargetInProgessState.TargetIdWrapper();
                newWrapper.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(originalWrapper.Equals(newWrapper));
                Assert.IsTrue(1 == newWrapper.id);

                originalWrapper = new TargetInProgessState.TargetIdWrapper();
                originalWrapper.id = 4;
                originalWrapper.name = string.Empty;
                originalWrapper.nodeId = 6;
                originalWrapper.projectId = 8;
                
                stream.Position = 0;
                // Serialize
                originalWrapper.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                newWrapper = new TargetInProgessState.TargetIdWrapper();
                newWrapper.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(originalWrapper.Equals(newWrapper));
                Assert.IsTrue(4 == newWrapper.id);

                originalWrapper = new TargetInProgessState.TargetIdWrapper();
                originalWrapper.id = 10;
                originalWrapper.name = null;
                originalWrapper.nodeId = 6;
                originalWrapper.projectId = 8;

                stream.Position = 0;
                // Serialize
                originalWrapper.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                newWrapper = new TargetInProgessState.TargetIdWrapper();
                newWrapper.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(originalWrapper.Equals(newWrapper));
                Assert.IsTrue(10 == newWrapper.id);
            }
            finally
            {
                // Close will close the writer/reader and the underlying stream
                writer.Close();
                reader.Close();
                reader = null;
                stream = null;
                writer = null;
            }

        }
    }
}
