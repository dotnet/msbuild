// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Text.RegularExpressions;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using Shouldly;

namespace Microsoft.Build.UnitTests.GenerateResource_Tests.OutOfProc
{
    [Trait("Category", "mono-osx-failing")]
    [Trait("Category", "mono-windows-failing")]
    sealed public class RequiredTransformations
    {
        private readonly ITestOutputHelper _output;

        public RequiredTransformations(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        ///  ResX to Resources, no references
        /// </summary>
        [Fact]
        public void BasicResX2Resources()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing BasicResX2Resources() test");

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                string resxFile = Utilities.WriteTestResX(false, null, null);
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.Sources[0].SetMetadata("Attribute", "InputValue");

                Utilities.ExecuteTask(t);

                Assert.Equal("InputValue", t.OutputResources[0].GetMetadata("Attribute"));
                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        /// Ensure that OutputResource Metadata is populated on the Sources item
        /// </summary>
        [Fact]
        public void OutputResourceMetadataPopulatedOnInputItems()
        {
            string resxFile0 = Utilities.WriteTestResX(false, null, null);
            string resxFile1 = Utilities.WriteTestResX(false, null, null);
            string resxFile2 = Utilities.WriteTestResX(false, null, null);
            string resxFile3 = Utilities.WriteTestResX(false, null, null);

            string expectedOutFile0 = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(resxFile0, ".resources"));
            string expectedOutFile1 = Path.Combine(Path.GetTempPath(), "resx1.foo.resources");
            string expectedOutFile2 = Path.Combine(Path.GetTempPath(), Utilities.GetTempFileName(".resources"));
            string expectedOutFile3 = Path.Combine(Path.GetTempPath(), Utilities.GetTempFileName(".resources"));

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.Sources = new ITaskItem[] {
                new TaskItem(resxFile0), new TaskItem(resxFile1), new TaskItem(resxFile2), new TaskItem(resxFile3) };

            t.OutputResources = new ITaskItem[] {
                new TaskItem(expectedOutFile0), new TaskItem(expectedOutFile1), new TaskItem(expectedOutFile2), new TaskItem(expectedOutFile3) };

            Utilities.ExecuteTask(t);

            Assert.Equal(expectedOutFile0, t.Sources[0].GetMetadata("OutputResource"));
            Assert.Equal(expectedOutFile1, t.Sources[1].GetMetadata("OutputResource"));
            Assert.Equal(expectedOutFile2, t.Sources[2].GetMetadata("OutputResource"));
            Assert.Equal(expectedOutFile3, t.Sources[3].GetMetadata("OutputResource"));

            // Done, so clean up.
            File.Delete(resxFile0);
            File.Delete(resxFile1);
            File.Delete(resxFile2);
            File.Delete(resxFile3);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Text to Resources
        /// </summary>
        [Fact]
        public void BasicText2Resources()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                string textFile = Utilities.WriteTestText(null, null);
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.Sources[0].SetMetadata("Attribute", "InputValue");

                Utilities.ExecuteTask(t);

                Assert.Equal("InputValue", t.OutputResources[0].GetMetadata("Attribute"));
                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  ResX to Resources with references that are used in the resx
        /// </summary>
        /// <remarks>System dll is not locked because it forces a new app domain</remarks>
        [Fact]
        public void ResX2ResourcesWithReferences()
        {
            string systemDll = Utilities.GetPathToCopiedSystemDLL();
            string resxFile = null;
            string resourcesFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

                resxFile = Utilities.WriteTestResX(true /*system type*/, null, null);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.References = new ITaskItem[] { new TaskItem(systemDll) };

                Utilities.ExecuteTask(t);

                resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Assert.Equal(t.FilesWritten[0].ItemSpec, resourcesFile);
            }
            finally
            {
                File.Delete(systemDll);
                if (resxFile != null) File.Delete(resxFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
            }
        }

        /// <summary>
        ///  Resources to ResX
        /// </summary>
        [Fact]
        public void BasicResources2ResX()
        {
            string resourcesFile = Utilities.CreateBasicResourcesFile(false, _output);

            // Fork 1: create a resx file directly from the resources
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };
            t.OutputResources = new ITaskItem[] { new TaskItem(Path.ChangeExtension(resourcesFile, ".resx")) };
            Utilities.ExecuteTask(t);
            Assert.Equal(".resx", Path.GetExtension(t.FilesWritten[0].ItemSpec));

            // Fork 2a: create a text file from the resources
            GenerateResource t2a = Utilities.CreateTaskOutOfProc(_output);
            t2a.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };
            t2a.OutputResources = new ITaskItem[] { new TaskItem(Path.ChangeExtension(resourcesFile, ".txt")) };
            Utilities.ExecuteTask(t2a);
            Assert.Equal(".txt", Path.GetExtension(t2a.FilesWritten[0].ItemSpec));

            // Fork 2b: create a resx file from the text file
            GenerateResource t2b = Utilities.CreateTaskOutOfProc(_output);
            t2b.Sources = new ITaskItem[] { new TaskItem(t2a.FilesWritten[0].ItemSpec) };
            t2b.OutputResources = new ITaskItem[] { new TaskItem(Utilities.GetTempFileName(".resx")) };
            Utilities.ExecuteTask(t2b);
            Assert.Equal(".resx", Path.GetExtension(t2b.FilesWritten[0].ItemSpec));

            // make sure the output resx files from each fork are the same
            Assert.Equal(Utilities.ReadFileContent(t.OutputResources[0].ItemSpec),
                                   Utilities.ReadFileContent(t2b.OutputResources[0].ItemSpec));

            // Done, so clean up.
            File.Delete(resourcesFile);
            File.Delete(t.OutputResources[0].ItemSpec);
            File.Delete(t2a.OutputResources[0].ItemSpec);
            foreach (ITaskItem item in t2b.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Resources to Text
        /// </summary>
        [Fact]
        public void BasicResources2Text()
        {
            string resourcesFile = Utilities.CreateBasicResourcesFile(false, _output);

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            t.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };

            string outputFile = Path.ChangeExtension(resourcesFile, ".txt");
            t.OutputResources = new ITaskItem[] { new TaskItem(outputFile) };
            Utilities.ExecuteTask(t);

            resourcesFile = t.FilesWritten[0].ItemSpec;
            Assert.Equal(".txt", Path.GetExtension(resourcesFile));
            Assert.Equal(Utilities.GetTestTextContent(null, null, true /*cleaned up */), Utilities.ReadFileContent(resourcesFile));

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Force out-of-date with ShouldRebuildResgenOutputFile on the source only
        /// </summary>
        [Fact]
        public void ForceOutOfDate()
        {
            string resxFile = Utilities.WriteTestResX(false, null, null);

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            try
            {
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);

                GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
                t2.StateFile = new TaskItem(t.StateFile);
                t2.Sources = new ITaskItem[] { new TaskItem(resxFile) };

                DateTime time = File.GetLastWriteTime(t.OutputResources[0].ItemSpec);
                System.Threading.Thread.Sleep(200);
                File.SetLastWriteTime(resxFile, DateTime.Now);

                Utilities.ExecuteTask(t2);

                Assert.True(DateTime.Compare(File.GetLastWriteTime(t2.OutputResources[0].ItemSpec), time) > 0);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  Force out-of-date with ShouldRebuildResgenOutputFile on the linked file
        /// </summary>
        [Fact]
        public void ForceOutOfDateLinked()
        {
            string bitmap = Utilities.CreateWorldsSmallestBitmap();
            string resxFile = Utilities.WriteTestResX(false, bitmap, null, false);

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            try
            {
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));

                Utilities.AssertStateFileWasWritten(t);

                GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
                t2.StateFile = new TaskItem(t.StateFile);
                t2.Sources = new ITaskItem[] { new TaskItem(resxFile) };

                DateTime time = File.GetLastWriteTime(t.OutputResources[0].ItemSpec);
                System.Threading.Thread.Sleep(200);
                File.SetLastWriteTime(bitmap, DateTime.Now);

                Utilities.ExecuteTask(t2);

                Assert.True(DateTime.Compare(File.GetLastWriteTime(t2.OutputResources[0].ItemSpec), time) > 0);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(bitmap);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  Force partially out-of-date: should build only the out of date inputs
        /// </summary>
        [Fact]
        public void ForceSomeOutOfDate()
        {
            string firstResx = null;
            string secondResx = null;
            string cache = null;

            try
            {
                firstResx = Utilities.WriteTestResX(false, null, null);
                secondResx = Utilities.WriteTestResX(false, null, null);
                cache = Utilities.GetTempFileName(".cache");

                GenerateResource createResources = Utilities.CreateTaskOutOfProc(_output);
                createResources.StateFile = new TaskItem(cache);
                createResources.Sources = new ITaskItem[] { new TaskItem(firstResx), new TaskItem(secondResx) };

                _output.WriteLine("Transform both");
                Utilities.ExecuteTask(createResources);

                _output.WriteLine("Get current write times of outputs");
                DateTime firstOutputCreationTime = File.GetLastWriteTime(createResources.OutputResources[0].ItemSpec);
                DateTime secondOutputCreationTime = File.GetLastWriteTime(createResources.OutputResources[1].ItemSpec);

                _output.WriteLine("Create a new task to transform them again");
                GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
                t2.StateFile = new TaskItem(createResources.StateFile.ItemSpec);
                t2.Sources = new ITaskItem[] { new TaskItem(firstResx), new TaskItem(secondResx) };

                System.Threading.Thread.Sleep(200);
                _output.WriteLine("Touch one input");
                File.SetLastWriteTime(firstResx, DateTime.Now);

                Utilities.ExecuteTask(t2);

                _output.WriteLine("Check only one output was updated");
                File.GetLastWriteTime(t2.OutputResources[0].ItemSpec).ShouldBeGreaterThan(firstOutputCreationTime);
                File.GetLastWriteTime(t2.OutputResources[1].ItemSpec).ShouldBe(secondOutputCreationTime);

                // Although only one file was updated, both should be in OutputResources and FilesWritten
                t2.OutputResources[0].ItemSpec.ShouldBe(createResources.OutputResources[0].ItemSpec);
                t2.OutputResources[1].ItemSpec.ShouldBe(createResources.OutputResources[1].ItemSpec);
                t2.FilesWritten[0].ItemSpec.ShouldBe(createResources.FilesWritten[0].ItemSpec);
                t2.FilesWritten[1].ItemSpec.ShouldBe(createResources.FilesWritten[1].ItemSpec);
            }
            finally
            {
                if (firstResx != null) File.Delete(firstResx);
                if (secondResx != null) File.Delete(secondResx);
                if (cache != null) File.Delete(cache);
                if (firstResx != null) File.Delete(Path.ChangeExtension(firstResx, ".resources"));
                if (secondResx != null) File.Delete(Path.ChangeExtension(secondResx, ".resources"));
            }
        }

        /// <summary>
        ///  Allow ShouldRebuildResgenOutputFile to return "false" since nothing's out of date, including linked file
        /// </summary>
        [Fact]
        public void AllowLinkedNoGenerate()
        {
            string bitmap = Utilities.CreateWorldsSmallestBitmap();
            string resxFile = Utilities.WriteTestResX(false, bitmap, null, false);

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            try
            {
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);

                DateTime time = File.GetLastWriteTime(t.OutputResources[0].ItemSpec);

                GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
                t2.StateFile = new TaskItem(t.StateFile);
                t2.Sources = new ITaskItem[] { new TaskItem(resxFile) };

                System.Threading.Thread.Sleep(200);

                Utilities.ExecuteTask(t2);

                Assert.Equal(time, File.GetLastWriteTime(t2.OutputResources[0].ItemSpec));
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(bitmap);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  Allow the task to skip processing based on having nothing out of date
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void NothingOutOfDate()
        {
            string resxFile = null;
            string txtFile = null;
            string resourcesFile1 = null;
            string resourcesFile2 = null;

            try
            {
                resxFile = Utilities.WriteTestResX(false, null, null);
                txtFile = Utilities.WriteTestText(null, null);

                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
                t.Sources = new ITaskItem[] { new TaskItem(resxFile), new TaskItem(txtFile) };
                resourcesFile1 = Path.ChangeExtension(resxFile, ".resources");
                resourcesFile2 = Path.ChangeExtension(txtFile, ".resources");

                Utilities.ExecuteTask(t);

                Assert.Equal(t.OutputResources[0].ItemSpec, resourcesFile1);
                Assert.Equal(t.FilesWritten[0].ItemSpec, resourcesFile1);
                Assert.Equal(t.OutputResources[1].ItemSpec, resourcesFile2);
                Assert.Equal(t.FilesWritten[1].ItemSpec, resourcesFile2);

                Utilities.AssertStateFileWasWritten(t);

                // Repeat, and it should do nothing as they are up to date
                GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
                t2.StateFile = new TaskItem(t.StateFile);
                t2.Sources = new ITaskItem[] { new TaskItem(resxFile), new TaskItem(txtFile) };

                DateTime time = File.GetLastWriteTime(t.OutputResources[0].ItemSpec);
                DateTime time2 = File.GetLastWriteTime(t.OutputResources[1].ItemSpec);
                System.Threading.Thread.Sleep(200);

                Utilities.ExecuteTask(t2);
                // Although everything was up to date, OutputResources and FilesWritten
                // must contain the files that would have been created if they weren't up to date.
                Assert.Equal(t2.OutputResources[0].ItemSpec, resourcesFile1);
                Assert.Equal(t2.FilesWritten[0].ItemSpec, resourcesFile1);
                Assert.Equal(t2.OutputResources[1].ItemSpec, resourcesFile2);
                Assert.Equal(t2.FilesWritten[1].ItemSpec, resourcesFile2);

                Utilities.AssertStateFileWasWritten(t2);

                Assert.Equal(time, File.GetLastWriteTime(t2.OutputResources[0].ItemSpec));
                Assert.Equal(time2, File.GetLastWriteTime(t2.OutputResources[1].ItemSpec));
            }
            finally
            {
                if (resxFile != null) File.Delete(resxFile);
                if (txtFile != null) File.Delete(txtFile);
                if (resourcesFile1 != null) File.Delete(resourcesFile1);
                if (resourcesFile2 != null) File.Delete(resourcesFile2);
            }
        }

        /// <summary>
        /// If the reference has been touched, it should rebuild even if the inputs are
        /// otherwise up to date
        /// </summary>
        /// <remarks>System dll is not locked because it forces a new app domain</remarks>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void NothingOutOfDateExceptReference()
        {
            string resxFile = null;
            string resourcesFile = null;
            string stateFile = Utilities.GetTempFileName(".cache");
            string localSystemDll = Utilities.GetPathToCopiedSystemDLL();

            try
            {
                resxFile = Utilities.WriteTestResX(true /* uses system type */, null, null);

                _output.WriteLine("** Running task to create resources.");

                GenerateResource initialCreator = Utilities.CreateTaskOutOfProc(_output);
                initialCreator.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                initialCreator.References = new ITaskItem[] { new TaskItem(localSystemDll) };
                initialCreator.StateFile = new TaskItem(stateFile);
                Utilities.ExecuteTask(initialCreator);

                DateTime firstWriteTime = File.GetLastWriteTime(initialCreator.OutputResources[0].ItemSpec);

                _output.WriteLine("** Repeat, and it should do nothing as they are up to date");

                GenerateResource incrementalUpToDate = Utilities.CreateTaskOutOfProc(_output);
                incrementalUpToDate.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                incrementalUpToDate.References = new ITaskItem[] { new TaskItem(localSystemDll) };
                incrementalUpToDate.StateFile = new TaskItem(stateFile);
                Utilities.ExecuteTask(incrementalUpToDate);

                File.GetLastWriteTime(incrementalUpToDate.OutputResources[0].ItemSpec).ShouldBe(firstWriteTime);

                _output.WriteLine("** Touch the reference, and repeat, it should now rebuild");

                DateTime newTime = DateTime.Now + new TimeSpan(0, 1, 0);
                File.SetLastWriteTime(localSystemDll, newTime);

                GenerateResource incrementalOutOfDate = Utilities.CreateTaskOutOfProc(_output);
                incrementalOutOfDate.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                incrementalOutOfDate.References = new ITaskItem[] { new TaskItem(localSystemDll) };
                incrementalOutOfDate.StateFile = new TaskItem(stateFile);
                Utilities.ExecuteTask(incrementalOutOfDate);

                File.GetLastWriteTime(incrementalOutOfDate.OutputResources[0].ItemSpec).ShouldBeGreaterThan(firstWriteTime);

                resourcesFile = incrementalOutOfDate.OutputResources[0].ItemSpec;
            }
            finally
            {
                if (resxFile != null) File.Delete(resxFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
                if (stateFile != null) File.Delete(stateFile);
                if (localSystemDll != null) File.Delete(localSystemDll);
            }
        }

        /// <summary>
        /// If an additional input is out of date, resources should be regenerated.
        /// </summary>
        [Fact]
        public void NothingOutOfDateExceptAdditionalInput()
        {
            string resxFile = null;
            string resourcesFile = null;
            ITaskItem[] additionalInputs = null;

            try
            {
                resxFile = Utilities.WriteTestResX(false, null, null);
                additionalInputs = new ITaskItem[] { new TaskItem(FileUtilities.GetTemporaryFile()), new TaskItem(FileUtilities.GetTemporaryFile()) };

                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.AdditionalInputs = additionalInputs;
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
                Utilities.ExecuteTask(t);

                DateTime time = File.GetLastWriteTime(t.OutputResources[0].ItemSpec);

                // Repeat, and it should do nothing as they are up to date
                GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
                t2.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t2.AdditionalInputs = additionalInputs;
                t2.StateFile = new TaskItem(t.StateFile);
                Utilities.ExecuteTask(t2);
                Utilities.AssertLogContainsResource(t2, "GenerateResource.NothingOutOfDate", "");

                // Touch one of the additional inputs and repeat, it should now rebuild
                DateTime newTime = DateTime.Now + new TimeSpan(0, 1, 0);
                File.SetLastWriteTime(additionalInputs[1].ItemSpec, newTime);
                GenerateResource t3 = Utilities.CreateTaskOutOfProc(_output);
                t3.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t3.AdditionalInputs = additionalInputs;
                t3.StateFile = new TaskItem(t.StateFile);
                Utilities.ExecuteTask(t3);
                Utilities.AssertLogNotContainsResource(t3, "GenerateResource.NothingOutOfDate", "");
                resourcesFile = t3.OutputResources[0].ItemSpec;
            }
            finally
            {
                if (resxFile != null) File.Delete(resxFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
                if (additionalInputs?[0] != null && File.Exists(additionalInputs[0].ItemSpec)) File.Delete(additionalInputs[0].ItemSpec);
                if (additionalInputs?[1] != null && File.Exists(additionalInputs[1].ItemSpec)) File.Delete(additionalInputs[1].ItemSpec);
            }
        }

        /// <summary>
        ///  Text to ResX
        /// </summary>
        [Fact]
        public void BasicText2ResX()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

            string textFile = Utilities.WriteTestText(null, null);
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            t.OutputResources = new ITaskItem[] { new TaskItem(Path.ChangeExtension(textFile, ".resx")) };

            Utilities.ExecuteTask(t);

            string resourcesFile = t.OutputResources[0].ItemSpec;
            Assert.Equal(".resx", Path.GetExtension(resourcesFile));

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Round trip from resx to resources to resx with the same blobs
        /// </summary>
        [Fact]
        public void ResX2ResX()
        {
            try
            {
                string resourcesFile = Utilities.CreateBasicResourcesFile(true, _output);

                // Step 1: create a resx file directly from the resources, to get a framework generated resx
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };
                t.OutputResources = new ITaskItem[] { new TaskItem(Path.ChangeExtension(resourcesFile, ".resx")) };
                Utilities.ExecuteTask(t);
                Assert.Equal(".resx", Path.GetExtension(t.FilesWritten[0].ItemSpec));

                // Step 2a: create a resources file from the resx
                GenerateResource t2a = Utilities.CreateTaskOutOfProc(_output);
                t2a.Sources = new ITaskItem[] { new TaskItem(t.FilesWritten[0].ItemSpec) };
                t2a.OutputResources = new ITaskItem[] { new TaskItem(Path.ChangeExtension(t.FilesWritten[0].ItemSpec, ".resources")) };
                Utilities.ExecuteTask(t2a);
                Assert.Equal(".resources", Path.GetExtension(t2a.FilesWritten[0].ItemSpec));

                // Step 2b: create a resx from the resources
                GenerateResource t2b = Utilities.CreateTaskOutOfProc(_output);
                t2b.Sources = new ITaskItem[] { new TaskItem(t2a.FilesWritten[0].ItemSpec) };
                t2b.OutputResources = new ITaskItem[] { new TaskItem(Utilities.GetTempFileName(".resx")) };
                File.Delete(t2b.OutputResources[0].ItemSpec);
                Utilities.ExecuteTask(t2b);
                Assert.Equal(".resx", Path.GetExtension(t2b.FilesWritten[0].ItemSpec));

                // make sure the output resx files from each fork are the same
                Assert.Equal(Utilities.ReadFileContent(t.OutputResources[0].ItemSpec),
                                       Utilities.ReadFileContent(t2b.OutputResources[0].ItemSpec));

                // Done, so clean up.
                File.Delete(resourcesFile);
                File.Delete(t.OutputResources[0].ItemSpec);
                File.Delete(t2a.OutputResources[0].ItemSpec);
                foreach (ITaskItem item in t2b.FilesWritten)
                    File.Delete(item.ItemSpec);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        ///  Round trip from text to resources to text with the same blobs
        /// </summary>
        [Fact]
        public void Text2Text()
        {
            string textFile = Utilities.WriteTestText(null, null);

            // Round 1, do the Text2Resource
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };

            Utilities.ExecuteTask(t);

            // make sure round 1 is successful
            string resourcesFile = t.OutputResources[0].ItemSpec;
            Assert.Equal(".resources", Path.GetExtension(resourcesFile));

            // round 2, do the resources2Text from the same file
            GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);

            t2.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };
            string outputFile = Utilities.GetTempFileName(".txt");
            t2.OutputResources = new ITaskItem[] { new TaskItem(outputFile) };
            Utilities.ExecuteTask(t2);

            resourcesFile = t2.FilesWritten[0].ItemSpec;
            Assert.Equal(".txt", Path.GetExtension(resourcesFile));

            Assert.Equal(Utilities.GetTestTextContent(null, null, true /*cleaned up */), Utilities.ReadFileContent(resourcesFile));

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
            File.Delete(t2.Sources[0].ItemSpec);
            foreach (ITaskItem item in t2.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  STR without references yields proper output, message
        /// </summary>
        [Fact]
        public void StronglyTypedResources()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            try
            {
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StronglyTypedLanguage = "CSharp";
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                // STR class name should have been generated from the output
                string stronglyTypedClassName = Path.GetFileNameWithoutExtension(t.OutputResources[0].ItemSpec);
                Assert.Equal(t.StronglyTypedClassName, stronglyTypedClassName);
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);
                // Files written should contain STR class file
                string stronglyTypedFileName = Path.ChangeExtension(t.Sources[0].ItemSpec, ".cs");
                Assert.Equal(t.FilesWritten[t.FilesWritten.Length - 1].ItemSpec, stronglyTypedFileName);
                Assert.True(File.Exists(stronglyTypedFileName));

                string typeName = null;
                if (t.StronglyTypedNamespace != null)
                    typeName = t.StronglyTypedNamespace + ".";
                else
                    typeName = "";

                typeName += t.StronglyTypedClassName;

                Utilities.AssertLogContains(t, typeName);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(t.StronglyTypedFileName);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  STR without references yields proper output, message
        /// </summary>
        [Fact]
        public void StronglyTypedResourcesUpToDate()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            GenerateResource t2 = Utilities.CreateTaskOutOfProc(_output);
            try
            {
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StronglyTypedLanguage = "CSharp";
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                // STR class name should have been generated from the output
                string stronglyTypedClassName = Path.GetFileNameWithoutExtension(t.OutputResources[0].ItemSpec);
                Assert.Equal(t.StronglyTypedClassName, stronglyTypedClassName);
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);
                // Files written should contain STR class file
                string stronglyTypedFileName = Path.ChangeExtension(t.Sources[0].ItemSpec, ".cs");
                Assert.Equal(t.FilesWritten[t.FilesWritten.Length - 1].ItemSpec, stronglyTypedFileName);
                Assert.True(File.Exists(stronglyTypedFileName));

                string typeName = null;
                if (t.StronglyTypedNamespace != null)
                    typeName = t.StronglyTypedNamespace + ".";
                else
                    typeName = "";

                typeName += t.StronglyTypedClassName;

                Utilities.AssertLogContains(t, typeName);

                // Now that we have done it, do it again to make sure that we don't do
                t2.StateFile = new TaskItem(t.StateFile);

                t2.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t2.StronglyTypedLanguage = "CSharp";

                Utilities.ExecuteTask(t2);

                Assert.Equal(t2.OutputResources[0].ItemSpec, resourcesFile);
                Assert.Equal(t2.FilesWritten[0].ItemSpec, resourcesFile);
                Utilities.AssertStateFileWasWritten(t2);
                Assert.Equal(t2.FilesWritten[t2.FilesWritten.Length - 1].ItemSpec, Path.ChangeExtension(t2.Sources[0].ItemSpec, ".cs"));
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(t.StronglyTypedFileName);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
                foreach (ITaskItem item in t2.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        /// STR class file is out of date, but resources are up to date. Should still generate it.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void StronglyTypedResourcesOutOfDate()
        {
            string resxFile = null;
            string resourcesFile = null;
            string strFile = null;
            string cacheFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                resxFile = Utilities.WriteTestResX(false, null, null);
                resourcesFile = Utilities.GetTempFileName(".resources");
                strFile = Path.ChangeExtension(resourcesFile, ".cs"); // STR filename should be generated from output not input filename
                cacheFile = Utilities.GetTempFileName(".cache");

                // Make sure the .cs file isn't already there.
                File.Delete(strFile);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.OutputResources = new ITaskItem[] { new TaskItem(resourcesFile) };
                t.StronglyTypedLanguage = "C#";
                t.StateFile = new TaskItem(cacheFile);
                Utilities.ExecuteTask(t);

                // STR class name generated from output resource file name
                string stronglyTypedClassName = Path.GetFileNameWithoutExtension(resourcesFile);
                Assert.Equal(t.StronglyTypedClassName, stronglyTypedClassName);
                resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Assert.True(File.Exists(resourcesFile));
                Assert.Equal(t.FilesWritten[t.FilesWritten.Length - 1].ItemSpec, strFile);
                Assert.True(File.Exists(strFile));

                // Repeat. It should not update either file.
                // First move both the timestamps back so they're still up to date,
                // but we'd know if they were updated (this is quicker than sleeping and okay as there's no cache being used)
                Utilities.MoveBackTimestamp(resxFile, 1);
                DateTime strTime = Utilities.MoveBackTimestamp(strFile, 1);
                t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.OutputResources = new ITaskItem[] { new TaskItem(resourcesFile) };
                t.StronglyTypedLanguage = "C#";
                t.StateFile = new TaskItem(cacheFile);
                Utilities.ExecuteTask(t);
                Assert.False(Utilities.FileUpdated(strFile, strTime)); // Was not updated

                // OK, now delete the STR class file
                File.Delete(strFile);

                // Repeat. It should recreate the STR class file
                t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.OutputResources = new ITaskItem[] { new TaskItem(resourcesFile) };
                t.StronglyTypedLanguage = "C#";
                t.StateFile = new TaskItem(cacheFile);
                Utilities.ExecuteTask(t);
                Assert.True(Utilities.FileUpdated(strFile, strTime)); // Was updated
                Assert.Equal(t.OutputResources[0].ItemSpec, resourcesFile);
                Assert.True(File.Exists(resourcesFile));
                Assert.Equal(t.FilesWritten[t.FilesWritten.Length - 1].ItemSpec, strFile);
                Assert.True(File.Exists(strFile));

                // OK, now delete the STR class file again
                File.Delete(strFile);

                // Repeat, but specify the filename this time, instead of having it generated from the output resources
                // It should recreate the STR class file again
                t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.OutputResources = new ITaskItem[] { new TaskItem(resourcesFile) };
                t.StronglyTypedLanguage = "C#";
                t.StronglyTypedFileName = strFile;
                Utilities.ExecuteTask(t);
                Assert.True(File.Exists(strFile));
            }
            finally
            {
                if (resxFile != null) File.Delete(resxFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
                if (strFile != null) File.Delete(strFile);
                if (cacheFile != null) File.Delete(cacheFile);
            }
        }

        /// <summary>
        /// Verify STR generation with a specified specific filename
        /// </summary>
        [Fact]
        public void StronglyTypedResourcesWithFilename()
        {
            string txtFile = null;
            string strFile = null;
            string resourcesFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

                txtFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.StronglyTypedLanguage = "CSharp";
                strFile = FileUtilities.GetTemporaryFile();
                t.StronglyTypedFileName = strFile;

                Utilities.ExecuteTask(t);

                // Check resources is output
                resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Assert.Single(t.OutputResources);
                Assert.Equal(".resources", Path.GetExtension(t.FilesWritten[0].ItemSpec));
                Assert.True(File.Exists(resourcesFile));

                // Check STR file is output
                Assert.Equal(t.FilesWritten[1].ItemSpec, strFile);
                Assert.Equal(t.StronglyTypedFileName, strFile);
                Assert.True(File.Exists(strFile));

                string typeName = "";
                if (t.StronglyTypedNamespace != null)
                {
                    typeName = t.StronglyTypedNamespace + ".";
                }

                typeName += t.StronglyTypedClassName;

                Utilities.AssertLogContains(t, typeName);
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
                if (strFile != null) File.Delete(strFile);
            }
        }

        /// <summary>
        ///  STR with VB
        /// </summary>
        [Fact]
        public void StronglyTypedResourcesVB()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            try
            {
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StronglyTypedLanguage = "VB";
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                // FilesWritten should contain STR class file
                string stronglyTypedFileName = Path.ChangeExtension(t.Sources[0].ItemSpec, ".vb");
                Assert.Equal(t.FilesWritten[t.FilesWritten.Length - 1].ItemSpec, stronglyTypedFileName);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Utilities.AssertStateFileWasWritten(t);
                Assert.True(File.Exists(stronglyTypedFileName));

                string typeName = null;
                if (t.StronglyTypedNamespace != null)
                    typeName = t.StronglyTypedNamespace + ".";
                else
                    typeName = "";

                typeName += t.StronglyTypedClassName;

                Utilities.AssertLogContains(t, typeName);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(t.StronglyTypedFileName);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  STR namespace can be empty
        /// </summary>
        [Fact]
        public void StronglyTypedResourcesWithoutNamespaceOrClassOrFilename()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StronglyTypedLanguage = "CSharp";
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                Utilities.ExecuteTask(t);

                string resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                resourcesFile = t.FilesWritten[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));

                Utilities.AssertStateFileWasWritten(t);

                // Should have defaulted the STR filename to the bare output resource name + ".cs"
                string STRfile = Path.ChangeExtension(t.Sources[0].ItemSpec, ".cs");
                Assert.Equal(t.StronglyTypedFileName, STRfile);
                Assert.True(File.Exists(STRfile));

                // Should have defaulted the class name to the bare output resource name
                Assert.Equal(t.StronglyTypedClassName, Path.GetFileNameWithoutExtension(t.OutputResources[0].ItemSpec));

                // Should not have used a namespace
                Assert.DoesNotContain("namespace", File.ReadAllText(t.StronglyTypedFileName));
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(t.StronglyTypedFileName);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  STR with resource namespace yields proper output, message (CS)
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void STRWithResourcesNamespaceCS()
        {
            Utilities.STRNamespaceTestHelper("CSharp", "MyResourcesNamespace", null, _output);
        }

        /// <summary>
        ///  STR with resource namespace yields proper output, message (VB)
        /// </summary>
        [Fact]
        public void STRWithResourcesNamespaceVB()
        {
            Utilities.STRNamespaceTestHelper("VB", "MyResourcesNamespace", null, _output);
        }

        /// <summary>
        ///  STR with resource namespace and STR namespace yields proper output, message (CS)
        /// </summary>
        [Fact]
        public void STRWithResourcesNamespaceAndSTRNamespaceCS()
        {
            Utilities.STRNamespaceTestHelper("CSharp", "MyResourcesNamespace", "MySTClassNamespace", _output);
        }

        /// <summary>
        ///  STR with resource namespace and STR namespace yields proper output, message (CS)
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void STRWithResourcesNamespaceAndSTRNamespaceVB()
        {
            Utilities.STRNamespaceTestHelper("VB", "MyResourcesNamespace", "MySTClassNamespace", _output);
        }
    }

    sealed public class TransformationErrors
    {
        private readonly ITestOutputHelper _output;

        public TransformationErrors(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        ///  Text input failures, no name, no '=', 'strings' token, invalid token, invalid escape
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void TextToResourcesBadFormat()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing TextToResourcesBadFormat() test");

            // The first string in each row is passed into the text block that's created in the file
            // The second string is a fragment of the expected error message
            string[][] tests = new string[][] {
                // invalid token in file, "unsupported square bracket keyword"
                new string[] {   "[goober]", "MSB3563" },
                // no '=', "resource line without an equals sign"
                new string[] {   "abcdefaghha", "MSB3564" },
                // no name, "resource line without a name"
                new string[] {   "=abced", "MSB3565" },
                // invalid escape, "unsupported or invalid escape character"
                new string[] {   "abc=de\\efght", "MSB3566" },
                // another invalid escape, this one more serious, "unsupported or invalid escape character"
                new string[] {   @"foo=\ujjjjbar", "MSB3569"},
            };
            GenerateResource t;
            string textFile;
            foreach (string[] test in tests)
            {
                t = Utilities.CreateTaskOutOfProc(_output);

                textFile = Utilities.WriteTestText(null, test[0]);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.Execute();

                // errors listed above -- boils down to resgen.exe error
                Utilities.AssertLogContains(t, "ERROR RG0000");

                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                foreach (ITaskItem item in t.FilesWritten)
                    File.Delete(item.ItemSpec);
            }

            // text file uses the strings token; since it's only a warning we have to have special asserts
            t = Utilities.CreateTaskOutOfProc(_output);

            textFile = Utilities.WriteTestText(null, "[strings]");
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            bool success = t.Execute();
            // Task should have succeeded (it was just a warning)
            Assert.True(success);
            // warning that 'strings' is an obsolete tag
            Utilities.AssertLogContains(t, "WARNING RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Cause failures in ResXResourceReader
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void FailedResXReader()
        {
            string resxFile1 = null;
            string resxFile2 = null;
            string resourcesFile1 = null;
            string resourcesFile2 = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                // Invalid one
                resxFile1 = Utilities.WriteTestResX(false, null, "  <data name='ack!'>>>>>>\xd\xa    <valueAB>Assembly</value>\xd\xa  </data>\xd\xa", false);
                // Also include a valid one. It should still get processed
                resxFile2 = Utilities.WriteTestResX(false, null, null);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile1), new TaskItem(resxFile2) };
                resourcesFile1 = Path.ChangeExtension(resxFile1, ".resources");
                resourcesFile2 = Path.ChangeExtension(resxFile2, ".resources");
                File.Delete(resourcesFile1);
                File.Delete(resourcesFile2);
                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                Utilities.AssertStateFileWasWritten(t);
                // Should not have created an output for the invalid resx
                // Should have created the other file
                Assert.False(File.Exists(resourcesFile1));
                Assert.Equal(t.OutputResources[0].ItemSpec, resourcesFile2);
                Assert.Single(t.OutputResources);
                Assert.Equal(t.FilesWritten[0].ItemSpec, resourcesFile2);
                Assert.True(File.Exists(resourcesFile2));

                // "error in resource file" with exception from the framework --
                // resgen.exe error
                Utilities.AssertLogContains(t, "ERROR RG0000");
            }
            finally
            {
                if (resxFile1 != null) File.Delete(resxFile1);
                if (resxFile2 != null) File.Delete(resxFile2);
                if (resourcesFile1 != null) File.Delete(resourcesFile1);
                if (resourcesFile2 != null) File.Delete(resourcesFile2);
            }
        }

        /// <summary>
        ///  Cause failures in ResXResourceReader, different codepath
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void FailedResXReaderWithAllOutputResourcesSpecified()
        {
            string resxFile1 = null;
            string resxFile2 = null;
            string resourcesFile1 = null;
            string resourcesFile2 = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                // Invalid one
                resxFile1 = Utilities.WriteTestResX(false, null, "  <data name='ack!'>>>>>>\xd\xa    <valueAB>Assembly</value>\xd\xa  </data>\xd\xa", false);
                // Also include a valid one. It should still get processed
                resxFile2 = Utilities.WriteTestResX(false, null, null);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile1), new TaskItem(resxFile2) };
                resourcesFile1 = Path.ChangeExtension(resxFile1, ".resources");
                resourcesFile2 = Path.ChangeExtension(resxFile2, ".resources");
                File.Delete(resourcesFile1);
                File.Delete(resourcesFile2);
                t.OutputResources = new ITaskItem[] { new TaskItem(resourcesFile1), new TaskItem(resourcesFile2) };

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                Utilities.AssertStateFileWasWritten(t);

                // Should not have created an output for the invalid resx
                // Should have created the other file
                Assert.False(File.Exists(resourcesFile1));
                Assert.Equal(t.OutputResources[0].ItemSpec, resourcesFile2);
                Assert.Single(t.OutputResources);
                Assert.Equal(t.FilesWritten[0].ItemSpec, resourcesFile2);
                Assert.True(File.Exists(resourcesFile2));

                // "error in resource file" with exception from the framework --
                // resgen.exe error
                Utilities.AssertLogContains(t, "ERROR RG0000");

                // so just look for the unlocalizable portions
                Utilities.AssertLogContains(t, "valueAB");
                Utilities.AssertLogContains(t, "value");
            }
            finally
            {
                if (resxFile1 != null) File.Delete(resxFile1);
                if (resxFile2 != null) File.Delete(resxFile2);
                if (resourcesFile1 != null) File.Delete(resourcesFile1);
                if (resourcesFile2 != null) File.Delete(resourcesFile2);
            }
        }

        /// <summary>
        ///  Duplicate resource names
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void DuplicateResourceNames()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            string textFile = Utilities.WriteTestText(null, "Marley=some guy from Jamaica");
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            bool success = t.Execute();
            // Task should have succeeded (it was just a warning)
            Assert.True(success);

            // "duplicate resource name" -- from resgen.exe
            Utilities.AssertLogContains(t, "WARNING RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Non-string resource with text output
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void UnsupportedTextType()
        {
            string bitmap = Utilities.CreateWorldsSmallestBitmap();
            string resxFile = Utilities.WriteTestResX(false, bitmap, null, false);

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
            t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
            t.OutputResources = new ITaskItem[] { new TaskItem(Path.ChangeExtension(resxFile, ".txt")) };
            bool success = t.Execute();
            // Task should have failed
            Assert.False(success);

            // "only strings can be written to a .txt file"
            // resgen.exe error
            Utilities.AssertLogContains(t, "ERROR RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            File.Delete(bitmap);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        /// Can't write the statefile
        /// </summary>
        [Fact]
        public void InvalidStateFile()
        {
            string resxFile = null;
            string resourcesFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                resxFile = Utilities.WriteTestResX(false, null, null);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.StateFile = new TaskItem("||invalid filename||");

                // Should still succeed
                Assert.True(t.Execute());

                resourcesFile = t.OutputResources[0].ItemSpec;
                Assert.Equal(".resources", Path.GetExtension(resourcesFile));
                Assert.Equal(t.FilesWritten[0].ItemSpec, t.OutputResources[0].ItemSpec);
            }
            finally
            {
                if (resxFile != null) File.Delete(resxFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
            }
        }

        /// <summary>
        ///  Cause failures in ResourceReader
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void FailedResourceReader()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

            // to cause a failure, we're going to transform a bad .resources file to a .resx
            // the simplest thing is to create a .resx, but call it .resources
            string resxFile = Utilities.WriteTestResX(false, null, null);
            string resourcesFile = Path.ChangeExtension(resxFile, ".resources");
            File.Move(resxFile, resourcesFile);
            t.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };
            t.OutputResources = new ITaskItem[] { new TaskItem(resxFile) };

            bool success = t.Execute();
            // Task should have failed
            Assert.False(success);

            // "error in resource file" with exception from the framework --
            // resgen.exe error
            Utilities.AssertLogContains(t, "ERROR RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Invalid STR Class name
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void FailedSTRProperty()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

            string textFile = Utilities.WriteTestText(null, "object=some string");

            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            t.StronglyTypedLanguage = "CSharp";
            // Invalid class name
            t.StronglyTypedClassName = "~!@#$%^&amp;*(";

            bool success = t.Execute();
            // Task should have failed
            Assert.False(success);

            // cannot write to STR class file -- resgen.exe error
            Utilities.AssertLogContains(t, "ERROR RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            File.Delete(t.StronglyTypedFileName);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        /// Reference passed in that can't be loaded should error
        /// </summary>
        [Fact]
        public void InvalidReference()
        {
            string txtFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

                // Create resx with invalid ref "INVALID"
                txtFile = Utilities.WriteTestResX(false, null, null, true /*data with invalid type*/);
                string resourcesFile = Path.ChangeExtension(txtFile, ".resources");
                File.Delete(resourcesFile);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.References = new TaskItem[] { new TaskItem("INVALID") };

                bool result = t.Execute();
                // Task should have failed
                Assert.False(result);

                // Should have not written any files
                Assert.True(t.FilesWritten?.Length == 0);
                Assert.False(File.Exists(resourcesFile));
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
            }
        }
    }

    sealed public class PropertyHandling
    {
        private readonly ITestOutputHelper _output;

        public PropertyHandling(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        ///  Sources attributes are copied to given OutputResources
        /// </summary>
        [Fact]
        public void AttributeForwarding()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing AttributeForwarding() test");

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            string resxFile = Utilities.WriteTestResX(false, null, null);
            ITaskItem i = new TaskItem(resxFile);
            i.SetMetadata("Locale", "en-GB");
            t.Sources = new ITaskItem[] { i };

            ITaskItem o = new TaskItem("MyAlternateResource.resources");
            o.SetMetadata("Locale", "fr");
            o.SetMetadata("Flavor", "Pumpkin");
            t.OutputResources = new ITaskItem[] { o };

            Utilities.ExecuteTask(t);

            // Locale was forward from source item and should overwrite the 'fr'
            // locale that the output item originally had.
            Assert.Equal("fr", t.OutputResources[0].GetMetadata("Locale"));

            // Output ItemSpec should not be overwritten.
            Assert.Equal("MyAlternateResource.resources", t.OutputResources[0].ItemSpec);

            // Attributes not on Sources should be left untouched.
            Assert.Equal("Pumpkin", t.OutputResources[0].GetMetadata("Flavor"));

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  Sources attributes copied to computed OutputResources
        /// </summary>
        [Fact]
        public void AttributeForwardingOnEmptyOutputs()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            string resxFile = Utilities.WriteTestResX(false, null, null);
            ITaskItem i = new TaskItem(resxFile);
            i.SetMetadata("Locale", "en-GB");
            t.Sources = new ITaskItem[] { i };

            Utilities.ExecuteTask(t);

            // Output ItemSpec should be computed from input
            string resourcesFile = Path.ChangeExtension(resxFile, ".resources");
            Assert.Equal(resourcesFile, t.OutputResources[0].ItemSpec);

            // Attribute from source should be copied to output
            Assert.Equal("en-GB", t.OutputResources[0].GetMetadata("Locale"));

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  OutputFiles used for output, and also are synthesized if not set on input
        /// </summary>
        [Fact]
        public void OutputFilesNotSpecified()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            t.Sources = new ITaskItem[] {
                new TaskItem( Utilities.WriteTestResX(false, null, null) ),
                new TaskItem( Utilities.WriteTestResX(false, null, null) ),
                new TaskItem( Utilities.WriteTestResX(false, null, null) ),
                new TaskItem( Utilities.WriteTestResX(false, null, null)),
            };

            Utilities.ExecuteTask(t);

            // Output ItemSpec should be computed from input
            for (int i = 0; i < t.Sources.Length; i++)
            {
                string outputFile = Path.ChangeExtension(t.Sources[i].ItemSpec, ".resources");
                Assert.Equal(outputFile, t.OutputResources[i].ItemSpec);
            }

            // Done, so clean up.
            foreach (ITaskItem item in t.Sources)
                File.Delete(item.ItemSpec);
            foreach (ITaskItem item in t.FilesWritten)
                File.Delete(item.ItemSpec);
        }

        /// <summary>
        ///  FilesWritten contains OutputResources + StateFile
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void FilesWrittenSet()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            t.Sources = new ITaskItem[] {
                new TaskItem( Utilities.WriteTestResX(false, null, null) ),
                new TaskItem( Utilities.WriteTestResX(false, null, null) ),
                new TaskItem( Utilities.WriteTestResX(false, null, null) ),
                new TaskItem( Utilities.WriteTestResX(false, null, null)),
            };

            t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

            Utilities.ExecuteTask(t);

            int i;
            for (i = 0; i < 4; i++)
            {
                Assert.Equal(t.FilesWritten[i].ItemSpec, t.OutputResources[i].ItemSpec);
                Assert.True(File.Exists(t.FilesWritten[i].ItemSpec));
            }

            Utilities.AssertStateFileWasWritten(t);

            // Done, so clean up.
            File.Delete(t.StateFile.ItemSpec);
            foreach (ITaskItem item in t.Sources)
            {
                File.Delete(item.ItemSpec);
            }
            foreach (ITaskItem item in t.FilesWritten)
            {
                File.Delete(item.ItemSpec);
            }
        }

        /// <summary>
        ///  Resource transformation fails on 3rd of 4 inputs, inputs 1 & 2 & 4 are in outputs and fileswritten.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void OutputFilesPartialInputs()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                t.Sources = new ITaskItem[] {
                new TaskItem( Utilities.WriteTestText(null, null) ),
                new TaskItem( Utilities.WriteTestText(null, null) ),
                new TaskItem( Utilities.WriteTestText("goober", null) ),
                new TaskItem( Utilities.WriteTestText(null, null)),
            };
                foreach (ITaskItem taskItem in t.Sources)
                {
                    File.Delete(Path.ChangeExtension(taskItem.ItemSpec, ".resources"));
                }

                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                string outputFile = Path.ChangeExtension(t.Sources[0].ItemSpec, ".resources");
                Assert.Equal(outputFile, t.OutputResources[0].ItemSpec);
                Assert.True(File.Exists(t.OutputResources[0].ItemSpec));
                outputFile = Path.ChangeExtension(t.Sources[1].ItemSpec, ".resources");
                Assert.Equal(outputFile, t.OutputResources[1].ItemSpec);
                Assert.True(File.Exists(t.OutputResources[1].ItemSpec));
                // Sources[2] should NOT have been converted and should not be in OutputResources
                outputFile = Path.ChangeExtension(t.Sources[2].ItemSpec, ".resources");
                Assert.False(File.Exists(outputFile));
                // Sources[3] should have been converted
                outputFile = Path.ChangeExtension(t.Sources[3].ItemSpec, ".resources");
                Assert.Equal(outputFile, t.OutputResources[2].ItemSpec);
                Assert.True(File.Exists(t.OutputResources[2].ItemSpec));

                // FilesWritten should contain only the 3 successfully output .resources and the cache
                Assert.Equal(t.FilesWritten[0].ItemSpec, Path.ChangeExtension(t.Sources[0].ItemSpec, ".resources"));
                Assert.Equal(t.FilesWritten[1].ItemSpec, Path.ChangeExtension(t.Sources[1].ItemSpec, ".resources"));
                Assert.Equal(t.FilesWritten[2].ItemSpec, Path.ChangeExtension(t.Sources[3].ItemSpec, ".resources"));
                Utilities.AssertStateFileWasWritten(t);

                // Make sure there was an error on the second resource
                // "unsupported square bracket keyword"
                Utilities.AssertLogContains(t, "ERROR RG0000");
                Utilities.AssertLogContains(t, "[goober]");
            }
            finally
            {
                // Done, so clean up.
                foreach (ITaskItem item in t.Sources)
                {
                    File.Delete(item.ItemSpec);
                }
                foreach (ITaskItem item in t.FilesWritten)
                {
                    File.Delete(item.ItemSpec);
                }
            }
        }

        /// <summary>
        ///  STR class name derived from output file transformation
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void StronglyTypedClassName()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StronglyTypedLanguage = "CSharp";
                t.StronglyTypedFileName = "somefile.cs";
                t.PublicClass = true;
                t.OutputResources = new ITaskItem[] { new TaskItem("somefile.resources") };

                Utilities.ExecuteTask(t);

                Assert.Equal(t.StronglyTypedClassName, Path.GetFileNameWithoutExtension(t.StronglyTypedFileName));
                // Verify class was public, as we specified
                Assert.Contains("public class " + t.StronglyTypedClassName, File.ReadAllText(t.StronglyTypedFileName));

                Utilities.AssertStateFileWasWritten(t);
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(t.StronglyTypedFileName);
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  STR class file name derived from class name transformation
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void StronglyTypedFileName()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StronglyTypedLanguage = "CSharp";
                File.Delete(Path.ChangeExtension(t.Sources[0].ItemSpec, ".cs"));

                Utilities.ExecuteTask(t);

                Utilities.AssertStateFileWasWritten(t);
                Assert.Equal(t.StronglyTypedFileName, Path.ChangeExtension(t.Sources[0].ItemSpec, ".cs"));
                Assert.True(File.Exists(t.StronglyTypedFileName));

                // Verify class was internal, since we didn't specify a preference
                Assert.Contains("internal class " + t.StronglyTypedClassName, File.ReadAllText(t.StronglyTypedFileName));
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.Delete(t.StronglyTypedFileName);

                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (File.Exists(item.ItemSpec))
                    {
                        File.Delete(item.ItemSpec);
                    }
                }
            }
        }
    }

    sealed public class PropertyErrors
    {
        private readonly ITestOutputHelper _output;

        public PropertyErrors(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        ///  Empty Sources yields message, success
        /// </summary>
        [Fact]
        public void EmptySources()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing EmptySources() test");

            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            Utilities.ExecuteTask(t);
            Utilities.AssertLogContainsResource(t, "GenerateResource.NoSources", "");
            if (t.FilesWritten != null)
            {
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (item.ItemSpec != null)
                        File.Delete(item.ItemSpec);
                }
            }
        }

        /// <summary>
        ///  References with invalid assemblies yields warning
        /// </summary>
        [Fact]
        public void ReferencesToBadAssemblies()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            string textFile = null;

            try
            {
                textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.References = new ITaskItem[] { new TaskItem("some non-existent DLL name goes here.dll") };
                bool success = t.Execute();

                // Resgen.exe attempts to consume the bad reference even if it's not
                // necessary, so task should fail
                Assert.False(success);
            }
            finally
            {
                // Done, so clean up.
                if (textFile != null)
                {
                    File.Delete(textFile);
                    File.Delete(Path.ChangeExtension(textFile, ".resources"));
                }
            }
        }

        /// <summary>
        ///  Source item not found
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void SourceItemMissing()
        {
            string txtFile = null;
            string resourcesFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                txtFile = Utilities.WriteTestText(null, null);
                resourcesFile = Path.ChangeExtension(txtFile, ".resources");
                File.Delete(resourcesFile);
                t.Sources = new ITaskItem[] { new TaskItem("non-existent.resx"), new TaskItem(txtFile) };
                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                // Invalid resx file: "Resource file cannot be found".
                Utilities.AssertLogContains(t, "ERROR MSB3552");

                // Should have processed remaining file
                Assert.Single(t.OutputResources);
                Assert.Equal(t.OutputResources[0].ItemSpec, resourcesFile);
                Assert.True(File.Exists(resourcesFile));
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
            }
        }

        /// <summary>
        ///  Non-existent StateFile yields message
        /// </summary>
        [Fact]
        [Trait("Category", "mono-windows-failing")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void StateFileUnwritable()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            try
            {
                string textFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(textFile) };
                t.StateFile = new TaskItem(FileUtilities.GetTemporaryFile());
                File.SetAttributes(t.StateFile.ItemSpec, FileAttributes.ReadOnly);
                t.Execute();

                // "cannot read state file (opening for read/write)"
                Utilities.AssertLogContains(t, "MSB3088");
                // "cannot write state file (opening for read/write)"
                Utilities.AssertLogContains(t, "MSB3101");
            }
            finally
            {
                // Done, so clean up.
                File.Delete(t.Sources[0].ItemSpec);
                File.SetAttributes(t.StateFile.ItemSpec, FileAttributes.Normal);
                if (t.FilesWritten != null)
                {
                    foreach (ITaskItem item in t.FilesWritten)
                    {
                        if (item.ItemSpec != null)
                            File.Delete(item.ItemSpec);
                    }
                }
            }
        }

        /// <summary>
        ///  Bad file extension on input
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void InputFileExtension()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            string textFile = Utilities.WriteTestText(null, null);
            string newTextFile = Path.ChangeExtension(textFile, ".foo");
            File.Move(textFile, newTextFile);
            t.Sources = new ITaskItem[] { new TaskItem(newTextFile) };

            t.Execute();

            // "unsupported file extension" -- An error from resgen.exe
            // should be in the log
            Utilities.AssertLogContains(t, "ERROR RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            if (t.FilesWritten != null)
            {
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (item.ItemSpec != null)
                        File.Delete(item.ItemSpec);
                }
            }
        }

        /// <summary>
        ///  Bad file extension on output
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void OutputFileExtension()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            string textFile = Utilities.WriteTestText(null, null);
            string resxFile = Path.ChangeExtension(textFile, ".foo");
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            t.OutputResources = new ITaskItem[] { new TaskItem(resxFile) };

            t.Execute();

            // "unsupported file extension" -- an error from resgen.exe should
            // be in the log
            Utilities.AssertLogContains(t, "ERROR RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            if (t.FilesWritten != null)
            {
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (item.ItemSpec != null)
                        File.Delete(item.ItemSpec);
                }
            }
        }

        /// <summary>
        ///  Sources and OutputResources different # of elements
        /// </summary>
        [Fact]
        public void SourcesMatchesOutputResources()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);

            string textFile = Utilities.WriteTestText(null, null);
            string resxFile = Path.ChangeExtension(textFile, ".resources");
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            t.OutputResources = new ITaskItem[] { new TaskItem(resxFile), new TaskItem("someother.resources") };

            t.Execute();

            // "two vectors must have the same length"
            Utilities.AssertLogContains(t, "MSB3094");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            if (t.FilesWritten != null)
            {
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (item.ItemSpec != null)
                        File.Delete(item.ItemSpec);
                }
            }
        }

        /// <summary>
        ///  Invalid StronglyTypedLanguage yields CodeDOM exception
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void UnknownStronglyTypedLanguage()
        {
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

            string textFile = Utilities.WriteTestText(null, null);
            t.Sources = new ITaskItem[] { new TaskItem(textFile) };
            t.StronglyTypedLanguage = "AkbarAndJeff";

            t.Execute();

            // "no codedom provider defined" -- An error from resgen.exe
            // should be in the log
            Utilities.AssertLogContains(t, "ERROR RG0000");

            // Done, so clean up.
            File.Delete(t.Sources[0].ItemSpec);
            if (t.FilesWritten != null)
            {
                foreach (ITaskItem item in t.FilesWritten)
                {
                    if (item.ItemSpec != null)
                        File.Delete(item.ItemSpec);
                }
            }
        }

        /// <summary>
        /// StronglyTypedLanguage, but more than one resources file
        /// </summary>
        [Fact]
        public void StronglyTypedResourceWithMoreThanOneInputResourceFile()
        {
            string resxFile = null;
            string resxFile2 = null;

            try
            {
                resxFile = Utilities.WriteTestResX(false, null, null);
                resxFile2 = Utilities.WriteTestResX(false, null, null);

                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile), new TaskItem(resxFile2) };
                t.StronglyTypedLanguage = "VisualBasic";

                Assert.False(t.Execute());

                // "str language but more than one source file"
                Utilities.AssertLogContains(t, "MSB3573");

                Assert.Empty(t.FilesWritten);
                Assert.True(t.OutputResources == null || t.OutputResources.Length == 0);
            }
            finally
            {
                if (resxFile != null) File.Delete(resxFile);
                if (resxFile2 != null) File.Delete(resxFile2);
                if (resxFile != null) File.Delete(Path.ChangeExtension(resxFile, ".resources"));
                if (resxFile2 != null) File.Delete(Path.ChangeExtension(resxFile2, ".resources"));
            }
        }

        /// <summary>
        ///  STR class name derived from output file transformation
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void BadStronglyTypedFilename()
        {
            string txtFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.StateFile = new TaskItem(Utilities.GetTempFileName(".cache"));

                txtFile = Utilities.WriteTestText(null, null);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.StronglyTypedLanguage = "CSharp";
                t.StronglyTypedClassName = "cc";
                t.StronglyTypedFileName = "||";
                t.OutputResources = new ITaskItem[] { new TaskItem("somefile.resources") };

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                // Other messages in InProc.PropertyErrors.BadStronglyTypedFilename() will not
                // show up because their equivalents (in sentiment but not exact syntax) will
                // be logged through resgen.exe instead.

                // We should get at least one error from resgen.exe because of the bad STR filename
                Utilities.AssertLogContains(t, "ERROR RG0000");

                // it didn't write the STR class successfully, so it shouldn't be in FilesWritten -- all we should see is
                // the statefile, because resgen.exe doesn't write the .resources file when STR creation fails
                Utilities.AssertStateFileWasWritten(t);
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
            }
        }

        /// <summary>
        /// Verify that passing a STR class without a language, errors
        /// </summary>
        [Fact]
        public void StronglyTypedResourceClassWithoutLanguage()
        {
            string txtFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                txtFile = Utilities.WriteTestText(null, null);
                string resourcesFile = Path.ChangeExtension(txtFile, ".resources");
                File.Delete(resourcesFile);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.StronglyTypedClassName = "myclassname";
                // no language

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                Utilities.AssertLogContainsResource(t, "GenerateResource.STRClassNamespaceOrFilenameWithoutLanguage");

                // Even the .resources wasn't created
                Assert.False(File.Exists(resourcesFile));
                Assert.Empty(t.FilesWritten);
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
            }
        }

        /// <summary>
        /// Verify that passing a STR namespace without a language, errors
        /// </summary>
        [Fact]
        public void StronglyTypedResourceNamespaceWithoutLanguage()
        {
            string txtFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                txtFile = Utilities.WriteTestText(null, null);
                string resourcesFile = Path.ChangeExtension(txtFile, ".resources");
                File.Delete(resourcesFile);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.StronglyTypedNamespace = "mynamespace";
                // no language

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                Utilities.AssertLogContainsResource(t, "GenerateResource.STRClassNamespaceOrFilenameWithoutLanguage");

                // Even the .resources wasn't created
                Assert.False(File.Exists(resourcesFile));
                Assert.Empty(t.FilesWritten);
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
            }
        }

        /// <summary>
        /// Verify that passing a STR filename without a language, errors
        /// </summary>
        [Fact]
        public void StronglyTypedResourceFilenameWithoutLanguage()
        {
            string txtFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                txtFile = Utilities.WriteTestText(null, null);
                string resourcesFile = Path.ChangeExtension(txtFile, ".resources");
                File.Delete(resourcesFile);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.StronglyTypedFileName = "myfile";
                // no language

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                Utilities.AssertLogContainsResource(t, "GenerateResource.STRClassNamespaceOrFilenameWithoutLanguage");

                // Even the .resources wasn't created
                Assert.False(File.Exists(resourcesFile));
                Assert.Empty(t.FilesWritten);
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
            }
        }

        /// <summary>
        /// Verify that passing a STR language with more than 1 sources errors
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void StronglyTypedResourceFileIsExistingDirectory()
        {
            string dir = null;
            string txtFile = null;
            string resourcesFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                txtFile = Utilities.WriteTestText(null, null);
                resourcesFile = Path.ChangeExtension(txtFile, ".resources");
                File.Delete(resourcesFile);
                string csFile = Path.ChangeExtension(txtFile, ".cs");
                File.Delete(csFile);
                t.Sources = new ITaskItem[] { new TaskItem(txtFile) };
                t.StronglyTypedLanguage = "C#";
                dir = Path.Combine(Path.GetTempPath(), "directory");
                Directory.CreateDirectory(dir);
                t.StronglyTypedFileName = dir;

                bool success = t.Execute();
                // Task should have failed
                Assert.False(success);

                // "AccessDeniedException" -- StronglyTypedFileName can't be
                // a directory
                Utilities.AssertLogContains(t, "ERROR RG0000");
                Utilities.AssertLogContains(t, t.StronglyTypedClassName);

                // Resgen.exe does not create either the resources or the STR file
                Assert.False(File.Exists(resourcesFile));
                Assert.False(File.Exists(csFile));
                Assert.Empty(t.FilesWritten);
            }
            finally
            {
                if (txtFile != null) File.Delete(txtFile);
                if (resourcesFile != null) File.Delete(resourcesFile);
                if (dir != null) FileUtilities.DeleteWithoutTrailingBackslash(dir);
            }
        }

        [Fact]
        public void Regress25163_OutputResourcesContainsInvalidPathCharacters()
        {
            string resourcesFile = null;

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                resourcesFile = Utilities.WriteTestResX(false, null, null);

                t.Sources = new ITaskItem[] { new TaskItem(resourcesFile) };
                t.OutputResources = new ITaskItem[] { new TaskItem(NativeMethodsShared.IsWindows ? "||" : "\0") };

                bool success = t.Execute();

                Assert.False(success); // "Task should have failed."

                // We will now hit the error earlier in task execution when checking for duplicates so we will not get resgen to even execute.
                Utilities.AssertLogContains(t, "MSB3553");
            }
            finally
            {
                if (resourcesFile != null) File.Delete(resourcesFile);
            }
        }
    }

    public class References
    {
        private readonly ITestOutputHelper _output;

        public References(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        //FIXME: mono: looks for csc.exe
        // https://github.com/Microsoft/msbuild/issues/677
        [Trait("Category", "mono-osx-failing")]
        public void DontLockP2PReferenceWhenResolvingSystemTypes()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing DontLockP2PReferenceWhenResolvingSystemTypes() test");

            // -------------------------------------------------------------------------------
            // Need to produce a .DLL assembly on disk, so we can pass it in as a reference to
            // GenerateResource.
            // -------------------------------------------------------------------------------
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("lib1.csproj", @"

                    <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <ProjectType>Local</ProjectType>
                            <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                            <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                            <AssemblyName>lib1</AssemblyName>
                            <OutputType>Library</OutputType>
                            <RootNamespace>lib1</RootNamespace>
                        </PropertyGroup>
                        <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                            <OutputPath>bin\Debug\</OutputPath>
                            <DebugSymbols>true</DebugSymbols>
                            <Optimize>false</Optimize>
                        </PropertyGroup>
                        <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                            <OutputPath>bin\Release\</OutputPath>
                            <DebugSymbols>false</DebugSymbols>
                            <Optimize>true</Optimize>
                        </PropertyGroup>
                        <ItemGroup>
                            <Reference Include=`System`/>
                            <Compile Include=`Class1.cs`/>
                        </ItemGroup>
                        <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                    </Project>

                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                    public class Class1
                    {
                    }
                ");

            MockLogger logger = new MockLogger(_output);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("lib1.csproj", logger);

            string p2pReference = Path.Combine(ObjectModelHelpers.TempProjectDir, @"bin\debug\lib1.dll");
            Assert.True(File.Exists(p2pReference)); // "lib1.dll doesn't exist."

            // -------------------------------------------------------------------------------
            // Done producing an assembly on disk.
            // -------------------------------------------------------------------------------

            // Create a .RESX that references unqualified (without an assembly name) System types.
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"MyStrings.resx", @"

                    <root>
                        <xsd:schema id=`root` xmlns=`` xmlns:xsd=`http://www.w3.org/2001/XMLSchema` xmlns:msdata=`urn:schemas-microsoft-com:xml-msdata`>
                            <xsd:element name=`root` msdata:IsDataSet=`true`>
                                <xsd:complexType>
                                    <xsd:choice maxOccurs=`unbounded`>
                                        <xsd:element name=`data`>
                                            <xsd:complexType>
                                                <xsd:sequence>
                                                    <xsd:element name=`value` type=`xsd:string` minOccurs=`0` msdata:Ordinal=`1` />
                                                    <xsd:element name=`comment` type=`xsd:string` minOccurs=`0` msdata:Ordinal=`2` />
                                                </xsd:sequence>
                                                <xsd:attribute name=`name` type=`xsd:string` />
                                                <xsd:attribute name=`type` type=`xsd:string` />
                                                <xsd:attribute name=`mimetype` type=`xsd:string` />
                                            </xsd:complexType>
                                        </xsd:element>
                                        <xsd:element name=`resheader`>
                                            <xsd:complexType>
                                                <xsd:sequence>
                                                    <xsd:element name=`value` type=`xsd:string` minOccurs=`0` msdata:Ordinal=`1` />
                                                </xsd:sequence>
                                                <xsd:attribute name=`name` type=`xsd:string` use=`required` />
                                            </xsd:complexType>
                                        </xsd:element>
                                    </xsd:choice>
                                </xsd:complexType>
                            </xsd:element>
                        </xsd:schema>
                        <resheader name=`ResMimeType`>
                            <value>text/microsoft-resx</value>
                        </resheader>
                        <resheader name=`Version`>
                            <value>1.0.0.0</value>
                        </resheader>
                        <resheader name=`Reader`>
                            <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                        </resheader>
                        <resheader name=`Writer`>
                            <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                        </resheader>
                        <data name=`GraphLegend` type=`System.String`>
                            <value>Graph Legend</value>
                            <comment>Used in reports to label the graph legend that pops up</comment>
                        </data>
                        <data name=`ccResponses` type=`System.String`>
                            <value>{0}'s Responses</value>
                            <comment>Used in challenge checklist tables</comment>
                        </data>
                        <data name=`ccStrength` type=`System.String`>
                            <value>Strength Area</value>
                            <comment>Used in challenge checklist tables</comment>
                        </data>
                        <data name=`ccNeutral` type=`System.String`>
                            <value>Neutral Area</value>
                            <comment>Used in challenge checklist tables</comment>
                        </data>
                        <data name=`ccChallenge` type=`System.String`>
                            <value>Challenge Area</value>
                            <comment>Used in challenge checklist tables</comment>
                        </data>
                        <data name=`calculation` type=`System.String`>
                            <value>Click here for scale calculation</value>
                            <comment>Used in Profile Scale area of main report to point to resource section scale tables.</comment>
                        </data>
                        <data name=`PageNumber` type=`System.String`>
                            <value>Page </value>
                            <comment>In footer of PDF report, and used in PDF links</comment>
                        </data>
                        <data name=`TOC` type=`System.String`>
                            <value>Table of Contents</value>
                            <comment>On second page of PDF report</comment>
                        </data>
                        <data name=`ParticipantListingAnd`>
                            <value>and</value>
                            <comment>On title page of PDF, joining two participants in a list</comment>
                        </data>
                    </root>

                ");

            // Run the GenerateResource task on the above .RESX file, passing in an unused reference
            // to lib1.dll.
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.Sources = new ITaskItem[] { new TaskItem(Path.Combine(ObjectModelHelpers.TempProjectDir, "MyStrings.resx")) };
            t.UseSourcePath = false;
            t.NeverLockTypeAssemblies = false;
            t.References = new ITaskItem[]
                {
                    new TaskItem(p2pReference),

                    // Path to System.dll
                    new TaskItem(new Uri((typeof(string)).Assembly.EscapedCodeBase).LocalPath)
                };

            bool success = t.Execute();

            // Make sure the resource was built.
            Assert.True(success); // "GenerateResource failed"
            ObjectModelHelpers.AssertFileExistsInTempProjectDirectory("MyStrings.resources");

            // Make sure the P2P reference is not locked after calling GenerateResource.
            File.Delete(p2pReference);
        }

        /// <summary>
        /// A reference is being passed into the
        /// GenerateResource task, but it's specified using a relative path.  GenerateResource
        /// was failing on this, because in the ResolveAssembly handler, it was calling
        /// Assembly.LoadFile on that relative path, which fails (LoadFile requires an
        /// absolute path).  The fix was to use Assembly.LoadFrom instead.
        /// </summary>
        [Fact]
        //FIXME: mono: looks for csc.exe
        // https://github.com/Microsoft/msbuild/issues/677
        [Trait("Category", "mono-osx-failing")]
        public void ReferencedAssemblySpecifiedUsingRelativePath()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing ReferencedAssemblySpecifiedUsingRelativePath() test");

            // -------------------------------------------------------------------------------
            // Need to produce a .DLL assembly on disk, so we can pass it in as a reference to
            // GenerateResource.
            // -------------------------------------------------------------------------------
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("ClassLibrary20.csproj", @"

                    <Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <ProjectType>Local</ProjectType>
                            <Configuration Condition=` '$(Configuration)' == '' `>Debug</Configuration>
                            <Platform Condition=` '$(Platform)' == '' `>AnyCPU</Platform>
                            <AssemblyName>ClassLibrary20</AssemblyName>
                            <OutputType>Library</OutputType>
                            <RootNamespace>lib1</RootNamespace>
                        </PropertyGroup>
                        <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' `>
                            <OutputPath>bin\Debug\</OutputPath>
                            <DebugSymbols>true</DebugSymbols>
                            <Optimize>false</Optimize>
                        </PropertyGroup>
                        <PropertyGroup Condition=` '$(Configuration)|$(Platform)' == 'Release|AnyCPU' `>
                            <OutputPath>bin\Release\</OutputPath>
                            <DebugSymbols>false</DebugSymbols>
                            <Optimize>true</Optimize>
                        </PropertyGroup>
                        <ItemGroup>
                            <Reference Include=`System`/>
                            <Compile Include=`Class1.cs`/>
                        </ItemGroup>
                        <Import Project=`$(MSBuildBinPath)\Microsoft.CSharp.targets` />
                    </Project>

                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("Class1.cs", @"
                    using System;
                    using System.Collections.Generic;
                    using System.Text;

                    namespace ClassLibrary20
                    {
                        [Serializable]
                        public class Class1
                        {
                            public string foo;
                        }
                    }
                ");

            MockLogger logger = new MockLogger(_output);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("ClassLibrary20.csproj", logger);

            // -------------------------------------------------------------------------------
            // Done producing an assembly on disk.
            // -------------------------------------------------------------------------------

            // Create a .RESX that references a type from ClassLibrary20.dll
            ObjectModelHelpers.CreateFileInTempProjectDirectory(@"MyStrings.resx", @"

                    <root>
                        <xsd:schema id=`root` xmlns=`` xmlns:xsd=`http://www.w3.org/2001/XMLSchema` xmlns:msdata=`urn:schemas-microsoft-com:xml-msdata`>
                            <xsd:element name=`root` msdata:IsDataSet=`true`>
                                <xsd:complexType>
                                    <xsd:choice maxOccurs=`unbounded`>
                                        <xsd:element name=`data`>
                                            <xsd:complexType>
                                                <xsd:sequence>
                                                    <xsd:element name=`value` type=`xsd:string` minOccurs=`0` msdata:Ordinal=`1` />
                                                    <xsd:element name=`comment` type=`xsd:string` minOccurs=`0` msdata:Ordinal=`2` />
                                                </xsd:sequence>
                                                <xsd:attribute name=`name` type=`xsd:string` />
                                                <xsd:attribute name=`type` type=`xsd:string` />
                                                <xsd:attribute name=`mimetype` type=`xsd:string` />
                                            </xsd:complexType>
                                        </xsd:element>
                                        <xsd:element name=`resheader`>
                                            <xsd:complexType>
                                                <xsd:sequence>
                                                    <xsd:element name=`value` type=`xsd:string` minOccurs=`0` msdata:Ordinal=`1` />
                                                </xsd:sequence>
                                                <xsd:attribute name=`name` type=`xsd:string` use=`required` />
                                            </xsd:complexType>
                                        </xsd:element>
                                    </xsd:choice>
                                </xsd:complexType>
                            </xsd:element>
                        </xsd:schema>
                        <resheader name=`ResMimeType`>
                            <value>text/microsoft-resx</value>
                        </resheader>
                        <resheader name=`Version`>
                            <value>1.0.0.0</value>
                        </resheader>
                        <resheader name=`Reader`>
                            <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                        </resheader>
                        <resheader name=`Writer`>
                            <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                        </resheader>
                        <data name=`Image1` type=`ClassLibrary20.Class1, ClassLibrary20, version=1.0.0.0, Culture=neutral, PublicKeyToken=null`>
                            <value>blah</value>
                        </data>
                    </root>

                ");

            // Run the GenerateResource task on the above .RESX file, passing in an unused reference
            // to lib1.dll.
            GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
            t.Sources = new ITaskItem[] { new TaskItem(Path.Combine(ObjectModelHelpers.TempProjectDir, "MyStrings.resx")) };
            t.UseSourcePath = false;
            t.NeverLockTypeAssemblies = false;

            TaskItem reference = new TaskItem(@"bin\debug\ClassLibrary20.dll");
            reference.SetMetadata("FusionName", "ClassLibrary20, version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            t.References = new ITaskItem[] { reference };

            // Set the current working directory to the location of ClassLibrary20.csproj.
            // This is what allows us to pass in a relative path to the referenced assembly.
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(ObjectModelHelpers.TempProjectDir);

            try
            {
                bool success = t.Execute();

                // Make sure the resource was built.
                Assert.True(success); // "GenerateResource failed"
                ObjectModelHelpers.AssertFileExistsInTempProjectDirectory("MyStrings.resources");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
            }
        }
    }

    public class MiscTests
    {
        private readonly ITestOutputHelper _output;

        public MiscTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ResgenCommandLineLogging()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing ResgenCommandLineLogging() test");

            // we use this to check if paths need quoting
            CommandLineBuilderHelper commandLineBuilderHelper = new CommandLineBuilderHelper();

            string resxFile = Utilities.WriteTestResX(false, null, null);
            string resourcesFile = Path.ChangeExtension(resxFile, ".resources");
            File.Delete(resourcesFile);

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.UseSourcePath = false;
                t.NeverLockTypeAssemblies = false;
                t.Execute();

                // Since this is resgen 4.0, will be in a response-file, which is line-delineated
                // and doesn't like spaces in filenames.
                Utilities.AssertLogContains(t, "/compile");
                Utilities.AssertLogContains(t, resxFile + "," + resourcesFile);
            }
            finally
            {
                File.Delete(resxFile);
                File.Delete(resourcesFile);
            }

            resxFile = Utilities.WriteTestResX(false, null, null);
            resourcesFile = Path.ChangeExtension(resxFile, ".resources");
            File.Delete(resourcesFile);

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.References = new ITaskItem[] { new TaskItem("baz"), new TaskItem("jazz") };
                t.UseSourcePath = true;
                t.PublicClass = true;
                t.StronglyTypedLanguage = "C#";
                t.NeverLockTypeAssemblies = false;
                t.Execute();

                string possiblyQuotedResxFile = resxFile;
                string possiblyQuotedResourcesFile = resourcesFile;

                if (commandLineBuilderHelper.DoesPathNeedQuotes(resxFile))
                {
                    possiblyQuotedResxFile = "\"" + resxFile + "\"";
                }

                if (commandLineBuilderHelper.DoesPathNeedQuotes(resourcesFile))
                {
                    possiblyQuotedResourcesFile = "\"" + resourcesFile + "\"";
                }

                Utilities.AssertLogContains(
                    t,
                    "/useSourcePath "
                    + "/publicClass "
                    + "/r:baz "
                    + "/r:jazz " + possiblyQuotedResxFile + " "
                    + possiblyQuotedResourcesFile + " " + "/str:\"C#\",,,");
            }
            finally
            {
                File.Delete(resxFile);
                File.Delete(resourcesFile);
                File.Delete(Path.ChangeExtension(resxFile, ".cs"));
            }

            resxFile = Utilities.WriteTestResX(false, null, null);
            resourcesFile = Path.ChangeExtension(resxFile, ".resources");
            File.Delete(resourcesFile);

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile) };
                t.References = new ITaskItem[] { new TaskItem("baz"), new TaskItem("jazz") };
                t.UseSourcePath = true;
                t.StronglyTypedLanguage = "C#";
                t.StronglyTypedClassName = "wagwag";
                t.StronglyTypedFileName = "boo";
                t.NeverLockTypeAssemblies = false;
                t.Execute();

                string possiblyQuotedResxFile = resxFile;
                string possiblyQuotedResourcesFile = resourcesFile;

                if (commandLineBuilderHelper.DoesPathNeedQuotes(resxFile))
                {
                    possiblyQuotedResxFile = "\"" + resxFile + "\"";
                }

                if (commandLineBuilderHelper.DoesPathNeedQuotes(resourcesFile))
                {
                    possiblyQuotedResourcesFile = "\"" + resourcesFile + "\"";
                }

                Utilities.AssertLogContains(
                    t,
                    "/useSourcePath "
                    + "/r:baz "
                    + "/r:jazz " + possiblyQuotedResxFile + " "
                    + possiblyQuotedResourcesFile + " "
                    + "/str:\"C#\",,wagwag,boo");
            }
            finally
            {
                File.Delete(resxFile);
                File.Delete(resourcesFile);
            }

            resxFile = Utilities.WriteTestResX(false, null, null);
            resourcesFile = Path.ChangeExtension(resxFile, ".myresources");
            File.Delete(resourcesFile);
            string resxFile1 = Utilities.WriteTestResX(false, null, null);
            string resourcesFile1 = Path.ChangeExtension(resxFile1, ".myresources");
            File.Delete(resourcesFile1);

            try
            {
                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = new ITaskItem[] { new TaskItem(resxFile), new TaskItem(resxFile1) };
                t.OutputResources = new ITaskItem[]
                                    {
                                        new TaskItem(resourcesFile),
                                        new TaskItem(resourcesFile1)
                                    };
                t.NeverLockTypeAssemblies = false;
                t.Execute();

                // Since this is resgen 4.0, will be in a response-file, which is line-delineated
                // and doesn't like spaces in filenames.
                Utilities.AssertLogContains(t, "/compile");
                Utilities.AssertLogContains(t, resxFile + "," + resourcesFile);
                Utilities.AssertLogContains(t, resxFile1 + "," + resourcesFile1);
            }
            finally
            {
                File.Delete(resxFile);
                File.Delete(resourcesFile);
                File.Delete(resxFile1);
                File.Delete(resourcesFile1);
            }
        }

        /// <summary>
        /// Validate that when using ResGen 3.5, a command line command where the last parameter takes us past the 28,000 character limit is handled appropriately
        /// </summary>
        [Fact]
        public void ResgenCommandLineExceedsAllowedLength()
        {
            string sdkToolsPath;
            string net35 = ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35);
            string net35sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.VersionLatest);
            // If .NET 3.5 isn't installed, then the ToolLocationHelper will either return null or there won't be an MSBuild subfolder under the Framework directory for .NET 3.5
            if (net35 != null && Directory.Exists(Path.Combine(net35, "MSBuild")) && net35sdk != null && Directory.Exists(Path.Combine(net35sdk, "bin")))
            {
                sdkToolsPath = Path.Combine(net35sdk, "bin");
            }
            else
            {
                Assert.True(true); // "We only need to test .NET 3.5 ResGen, if it isn't on disk then pass the test and return"
                return;
            }

            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing ResgenCommandLineExceedsAllowedLength() test");

            // we use this to check if paths need quoting
            CommandLineBuilderHelper commandLineBuilderHelper = new CommandLineBuilderHelper();

            List<ITaskItem> sources = new List<ITaskItem>();
            List<ITaskItem> outputResources = new List<ITaskItem>();

            try
            {
                int filesToBeCreated = 83;

                // The filesToBeCreated number is determined from the Username length and the given explicitly set temp folder.
                // These numbers were shown through trial and error to be the correct numbers.
                switch (Environment.UserName.Length)
                {
                    case 1:
                    case 2:
                        filesToBeCreated = 89;
                        break;
                    case 3:
                    case 4:
                    case 5:
                        filesToBeCreated = 88;
                        break;
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                        filesToBeCreated = 87;
                        break;
                    case 10:
                    case 11:
                    case 12:
                    case 13:
                        filesToBeCreated = 86;
                        break;
                    case 14:
                    case 15:
                    case 16:
                    case 17:
                        filesToBeCreated = 85;
                        break;
                    case 18:
                    case 19:
                    case 20:
                        filesToBeCreated = 84;
                        break;
                }

                // Get the generic "Temp" folder from the users' LocalAppData path in case they specify a different "Temp" folder or it's
                // located on a drive other than C.
                string tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "temp");

                // This loop creates "filesToBeCreated" source files (+ output files) of 140 characters each such that the last one exceeds
                // the 28,000 command line max length limit in order to validate that resgen is behaving properly in that scenario
                for (int x = 0; x < filesToBeCreated; x++)
                {
                    string fileName = new String('c', 133) + String.Format("{0:00}", x);
                    string resxFile = MyResxFileCreator(tempFolder, fileName);
                    string resourcesFile = Path.ChangeExtension(fileName, ".resources");
                    sources.Add(new TaskItem(resxFile));
                    outputResources.Add(new TaskItem(resourcesFile));
                    File.Delete(resourcesFile);
                }

                GenerateResource t = Utilities.CreateTaskOutOfProc(_output);
                t.Sources = sources.ToArray();
                t.OutputResources = outputResources.ToArray();
                t.StronglyTypedLanguage = null;
                t.UseSourcePath = false;
                t.NeverLockTypeAssemblies = false;
                t.SdkToolsPath = sdkToolsPath;
                Assert.True(t.Execute()); // "Task should have completed successfully"

                Utilities.AssertLogContains(t, "/compile");
                foreach (ITaskItem i in sources)
                {
                    Utilities.AssertLogContains(t, i.ItemSpec);
                }
                foreach (ITaskItem i in outputResources)
                {
                    Utilities.AssertLogContains(t, i.ItemSpec);
                }
            }
            finally
            {
                foreach (ITaskItem i in sources)
                {
                    File.Delete(i.ItemSpec);
                }

                foreach (ITaskItem i in outputResources)
                {
                    File.Delete(i.ItemSpec);
                }
            }
        }

        /// <summary>
        /// Personalized resx creator.
        /// </summary>
        /// <param name="pathName">Path in which to create the resx file</param>
        /// <param name="fileName">File name of the created resx</param>
        /// <returns>Path to the resx file</returns>
        private string MyResxFileCreator(string pathName, string fileName)
        {
            Directory.CreateDirectory(pathName);
            string resgenFile = Path.Combine(pathName, fileName + ".resx");
            if (File.Exists(resgenFile))
            {
                File.Delete(resgenFile);
            }
            File.WriteAllText(resgenFile, Utilities.GetTestResXContent(false, null, null, false));
            return resgenFile;
        }

        /// <summary>
        /// In order to make GenerateResource multitargetable, a property, ExecuteAsTool, was added.
        /// In order to have correct behavior when using pre-4.0
        /// toolsversions, ExecuteAsTool must default to true, and the paths to the tools will be the
        /// v3.5 path.  It is difficult to verify the tool paths in a unit test, however, so
        /// this was done by ad hoc testing and will be maintained by the dev suites.
        /// </summary>
        [Fact]
        public void MultiTargetingDefaultsSetCorrectly()
        {
            GenerateResource t = new GenerateResource();

            Assert.True(t.ExecuteAsTool); // "ExecuteAsTool should default to true"
        }
    }
}

namespace Microsoft.Build.UnitTests.GenerateResource_Tests
{
    /// <summary>
    /// This Utilities class provides some static helper methods for resource tests
    /// </summary>
    internal sealed partial class Utilities
    {
        /// <summary>
        /// This method creates a GenerateResource task and performs basic setup on it, e.g. BuildEngine
        /// </summary>
        /// <param name="output"></param>
        public static GenerateResource CreateTaskOutOfProc(ITestOutputHelper output)
        {
            GenerateResource t = CreateTask(output);
            t.ExecuteAsTool = true;
            t.SdkToolsPath = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.VersionLatest);

            return t;
        }

        /// <summary>
        /// Asserts if the passed in ITaskItem array contains any items that are not tlogs
        /// </summary>
        /// <param name="filesWritten"></param>
        public static void AssertContainsOnlyTLogs(ITaskItem[] filesWritten)
        {
            foreach (ITaskItem file in filesWritten)
            {
                Assert.Equal(".tlog", Path.GetExtension(file.ItemSpec)); // "The only files written should be tlogs"
            }
        }
    }
}
