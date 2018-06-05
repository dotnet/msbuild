// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>ToolTask that wraps ResGen.exe, which transforms resource files.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using CodeDomProvider = System.CodeDom.Compiler.CodeDomProvider;
using MSBuildProcessorArchitecture = Microsoft.Build.Utilities.ProcessorArchitecture;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This class defines the "GenerateResource" MSBuild task, which enables using resource APIs
    /// to transform resource files.
    /// </summary>
    /// <comment>See GenerateResource.cs for the source code to the GenerateResource task; this file
    /// just contains the nested internal ResGen task</comment>
    public sealed partial class GenerateResource : TaskExtension
    {
        /// <summary>
        /// Defines the "ResGen" MSBuild task, which enables using ResGen.exe 
        /// to generate strongly-typed resource classes and convert resource
        /// files from one format to another.
        /// </summary>
        internal class ResGen : ToolTaskExtension
        {
            #region Properties
            /*
        Microsoft (R) .NET Resource Generator
        [Microsoft .Net Framework, Version 4.0.10719.0]
        Copyright (c) Microsoft Corporation.  All rights reserved.

        Usage:
           ResGen inputFile.ext [outputFile.ext] [/str:lang[,namespace[,class[,file]]]]
           ResGen [options] /compile inputFile1.ext[,outputFile1.resources] [...]

        Where .ext is .resX, .restext, .txt, or .resources

        Converts files from one resource format to another.  If the output
        filename is not specified, inputFile.resources will be used.
        Options:
        /compile        Converts a list of resource files from one format to another
                        in one bulk operation.  By default, it converts into .resources
                        files, using inputFile[i].resources for the output file name.
        /str:<language>[,<namespace>[,<class name>[,<file name>]]]]
                        Creates a strongly-typed resource class in the specified
                        programming language using CodeDOM. In order for the strongly
                        typed resource class to work properly, the name of your output
                        file without the .resources must match the
                        [namespace.]classname of your strongly typed resource class.
                        You may need to rename your output file before using it or
                        embedding it into an assembly.
        /useSourcePath  Use each source file's directory as the current directory
                        for resolving relative file paths.
        /publicClass    Create the strongly typed resource class as a public class.
                        This option is ignored if the /str: option is not used.
        /r:<assembly>   Load types from these assemblies. A ResX file with a previous
                        version of a type will use the one in this assembly, when set.
        /define:A[,B]   For #ifdef support in .ResText files, pass a comma-separated
                        list of symbols.  ResText files can use "#ifdef A" or "#if !B".
        
        Miscellaneous:
        @<file>         Read response file for more options. At most one response file
                        may be specified, and its entries must be line-separated.
        
        .restext & .txt files have this format:
        
            # Use # at the beginning of a line for a comment character.
            name=value
            more elaborate name=value
        
        Example response file contents:
        
            # Use # at the beginning of a line for a comment character.
            /useSourcePath
            /compile
            file1.resx,file1.resources
            file2.resx,file2.resources
        

        Language names valid for the /str:<language> option are:
        c#, cs, csharp, vb, vbs, visualbasic, vbscript, js, jscript, javascript, vj#, vjs, vjsharp, c++, mc, cpp
         */

            /// <summary>
            /// Files being passed to ResGen.exe to be converted to a different resource format.  
            /// If a strongly typed resource class is being created, only one file may be 
            /// passed to InputFiles at a time. 
            /// </summary>
            public ITaskItem[] InputFiles
            {
                get => (ITaskItem[])Bag[nameof(InputFiles)];
                set => Bag[nameof(InputFiles)] = value;
            }

            /// <summary>
            /// Should be the same length as InputFiles or null.  If null, the files output
            /// by ResGen.exe will be named "inputFiles[i].resources".  Otherwise, the 
            /// extensions on the output filesnames indicate which format the corresponding
            /// input file will be translated to.
            /// </summary>
            public ITaskItem[] OutputFiles
            {
                get => (ITaskItem[])Bag[nameof(OutputFiles)];
                set => Bag[nameof(OutputFiles)] = value;
            }

            /// <summary>
            /// Specifies whether the strongly typed class should be created public (with public methods)
            /// instead of the default internal. Analogous to resgen.exe's /publicClass switch.
            /// </summary>
            public bool PublicClass
            {
                get => GetBoolParameterWithDefault(nameof(PublicClass), false);
                set => Bag[nameof(PublicClass)] = value;
            }

            /// <summary>
            /// Resolves types in ResX files (XML resources) for Strongly Typed Resources
            /// </summary>
            public ITaskItem[] References
            {
                get => (ITaskItem[])Bag[nameof(References)];
                set => Bag[nameof(References)] = value;
            }

            /// <summary>
            /// Path to the SDK directory where ResGen.exe can be found
            /// </summary>
            public string SdkToolsPath
            {
                get => (string)Bag[nameof(SdkToolsPath)];
                set => Bag[nameof(SdkToolsPath)] = value;
            }

            /// <summary>
            /// The language to use when generating the class source for the strongly typed resource.
            /// This parameter must match exactly one of the languages used by the CodeDomProvider.
            /// </summary>
            public string StronglyTypedLanguage
            {
                get => (string)Bag[nameof(StronglyTypedLanguage)];
                set => Bag[nameof(StronglyTypedLanguage)] = value;
            }

            /// <summary>
            /// Specifies the namespace to use for the generated class source for the
            /// strongly typed resource. If left blank, no namespace is used.
            /// </summary>
            public string StronglyTypedNamespace
            {
                get => (string)Bag[nameof(StronglyTypedNamespace)];
                set => Bag[nameof(StronglyTypedNamespace)] = value;
            }

            /// <summary>
            /// Specifies the class name for the strongly typed resource class.  If left blank, the base
            /// name of the resource file is used.
            /// </summary>
            public string StronglyTypedClassName
            {
                get => (string)Bag[nameof(StronglyTypedClassName)];
                set => Bag[nameof(StronglyTypedClassName)] = value;
            }

            /// <summary>
            /// Specifies the filename for the source file.  If left blank, the name of the class is
            /// used as the base filename, with the extension dependent on the language.
            /// </summary>
            public string StronglyTypedFileName
            {
                get => (string)Bag[nameof(StronglyTypedFileName)];
                set => Bag[nameof(StronglyTypedFileName)] = value;
            }

            /// <summary>
            /// Indicates whether the resource reader should use the source file's directory to
            /// resolve relative file paths.
            /// </summary>
            public bool UseSourcePath
            {
                get => GetBoolParameterWithDefault(nameof(UseSourcePath), false);
                set => Bag[nameof(UseSourcePath)] = value;
            }

            #endregion // Properties

            #region ToolTask Members

            /// <summary>
            /// Returns the name of the tool to execute
            /// </summary>
            protected override string ToolName => "resgen.exe";

            /// <summary>
            /// Tracker.exe wants Unicode response files, and ResGen.exe doesn't care, 
            /// so make them Unicode across the board. 
            /// </summary>
            /// <comment>
            /// We no longer use Tracker.exe in ResGen, but given that as ResGen doesn't care, 
            /// there doesn't really seem to be a particular reason to change it back, either...
            /// </comment>
            protected override Encoding ResponseFileEncoding => Encoding.Unicode;

            /// <summary>
            /// Invokes the ToolTask with the given parameters
            /// </summary>
            /// <returns>True if the task succeeded, false otherwise</returns>
            public override bool Execute()
            {
                // If there aren't any input resources, well, we've already succeeded!
                if (IsNullOrEmpty(InputFiles))
                {
                    Log.LogMessageFromResources(MessageImportance.Low, "ResGen.NoInputFiles");
                    return !Log.HasLoggedErrors;
                }

                if (IsNullOrEmpty(OutputFiles))
                {
                    GenerateOutputFileNames();
                }

                bool success;

                // if command line is too long, fail
                string commandLineCommands = GenerateCommandLineCommands();

                // when comparing command line length, need to add one for leading space added between command arguments and tool name
                if (!string.IsNullOrEmpty(commandLineCommands) && (commandLineCommands.Length + 1) > s_maximumCommandLength)
                {
                    Log.LogErrorWithCodeFromResources("ResGen.CommandTooLong", commandLineCommands.Length);
                    success = false;
                }
                else
                {
                    // Use ToolTaskExtension's Execute()
                    success = base.Execute();
                }

                if (String.IsNullOrEmpty(StronglyTypedLanguage))
                {
                    if (!success)
                    {
                        // One or more of the generated resources was not, in fact generated --
                        // only keep in OutputFiles the ones that actually exist.
                        ITaskItem[] outputFiles = OutputFiles;
                        var successfullyGenerated = new List<ITaskItem>();

                        for (int i = 0; i < outputFiles.Length; i++)
                        {
                            if (File.Exists(outputFiles[i].ItemSpec))
                            {
                                successfullyGenerated.Add(outputFiles[i]);
                            }
                        }

                        OutputFiles = successfullyGenerated.ToArray();
                    }
                }
                else
                {
                    ITaskItem outputFile = OutputFiles[0];

                    // if the resource generation was unsuccessful, check to see that the resource file 
                    // was in fact generated
                    if (!success)
                    {
                        if (!File.Exists(outputFile.ItemSpec))
                        {
                            OutputFiles = Array.Empty<ITaskItem>();
                        }
                    }

                    // Default the class name if we need to - regardless of whether the STR was successfully generated
                    if (StronglyTypedClassName == null)
                    {
                        StronglyTypedClassName = Path.GetFileNameWithoutExtension(outputFile.ItemSpec);
                    }

                    // Default the filename if we need to - regardless of whether the STR was successfully generated
                    if (StronglyTypedFileName == null)
                    {
                        CodeDomProvider provider;
                        try
                        {
                            provider = CodeDomProvider.CreateProvider(StronglyTypedLanguage);
                        }
                        catch (System.Configuration.ConfigurationException)
                        {
                            // If the language can't be found, then ResGen.exe will already have 
                            // logged an appropriate error.  
                            return false;
                        }
                        catch (System.Security.SecurityException)
                        {
                            // If the language can't be found, then ResGen.exe will already have 
                            // logged an appropriate error.  
                            return false;
                        }

                        StronglyTypedFileName = ProcessResourceFiles.GenerateDefaultStronglyTypedFilename(provider, outputFile.ItemSpec);
                    }
                }

                return success && !Log.HasLoggedErrors;
            }

            /// <summary>
            /// Fills the provided CommandLineBuilderExtension with all the command line options used when
            /// executing this tool that can go into a response file.  
            /// </summary>
            /// <comments>
            /// ResGen 3.5 and earlier doesn't support response files, but ResGen 4.0 and later does.
            /// </comments>
            /// <param name="commandLine">Gets filled with command line options</param>
            protected internal override void AddResponseFileCommands(CommandLineBuilderExtension commandLine)
            {
                string pathToResGen = GenerateResGenFullPath();

                // Only do anything if we can actually use response files
                if (pathToResGen != null
                    && NativeMethodsShared.IsWindows
                    && !pathToResGen.Equals(
                        ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(
                            "resgen.exe",
                            TargetDotNetFrameworkVersion.Version35),
                            StringComparison.OrdinalIgnoreCase)
                    && String.IsNullOrEmpty(StronglyTypedLanguage))
                {
                    // 4.0 resgen.exe does support response files, so we can return the resgen arguments here!
                    var resGenArguments = new CommandLineBuilderExtension();
                    GenerateResGenCommands(resGenArguments, true /* arguments must be line-delimited */);

                    commandLine.AppendTextUnquoted(resGenArguments.ToString());
                }
                else
                {
                    // return nothing -- if it's not 4.0, or if we're building strongly typed resources, we assume that, 
                    // as far as ToolTask is concerned at least, response files are not supported. 
                }
            }

            /// <summary>
            /// Fills the provided CommandLineBuilderExtension with all the command line options used when
            /// executing this tool that must go on the command line
            /// </summary>
            /// <comments>
            /// Has to be command line commands because ResGen 3.5 and earlier don't know about
            /// response files. 
            /// </comments>
            /// <param name="commandLine">Gets filled with command line options</param>
            protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
            {
                ErrorUtilities.VerifyThrow(!IsNullOrEmpty(InputFiles), "If InputFiles is empty, the task should have returned before reaching this point");

                var resGenArguments = new CommandLineBuilderExtension();
                GenerateResGenCommands(resGenArguments, false /* don't line-delimit arguments; spaces are just fine */);

                string pathToResGen = GenerateResGenFullPath();

                if (pathToResGen != null &&
                    NativeMethodsShared.IsWindows &&
                    !pathToResGen.Equals(NativeMethodsShared.GetLongFilePath(ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("resgen.exe", TargetDotNetFrameworkVersion.Version35)), StringComparison.OrdinalIgnoreCase) &&
                    String.IsNullOrEmpty(StronglyTypedLanguage))
                {
                    // 4.0 resgen.exe does support response files (at least as long as you're not building an STR), so we can 
                    // make use of them here by returning nothing!
                }
                else
                {
                    // otherwise, the toolname is ResGen.exe and we just need the resgen arguments in CommandLineCommands. 
                    commandLine.AppendTextUnquoted(resGenArguments.ToString());
                }
            }

            /// <summary>
            /// Generates the full path to the tool being executed by this ToolTask
            /// </summary>
            /// <returns>A string containing the full path of this tool, or null if the tool was not found</returns>
            protected override string GenerateFullPathToTool()
            {
                // Use ToolPath if it exists.
                string pathToTool = GenerateResGenFullPath();
                return pathToTool;
            }

            /// <summary>
            /// Validates the parameters passed to the task
            /// </summary>
            /// <returns>True if parameters are valid</returns>
            protected override bool ValidateParameters()
            {
                ErrorUtilities.VerifyThrow(!IsNullOrEmpty(InputFiles), "If InputFiles is empty, the task should have returned before reaching this point");

                // make sure that if the output resources were set, they exactly match the number of input sources
                if (!IsNullOrEmpty(OutputFiles) && (OutputFiles.Length != InputFiles.Length))
                {
                    Log.LogErrorWithCodeFromResources("General.TwoVectorsMustHaveSameLength", InputFiles.Length, OutputFiles.Length, "InputFiles", "OutputFiles");
                    return false;
                }

                // Creating an STR is triggered merely by setting the language
                if (!String.IsNullOrEmpty(StronglyTypedLanguage))
                {
                    // Only a single Sources is allowed if you are generating STR.
                    // Otherwise, each STR class overwrites the previous one. In theory we could generate separate 
                    // STR classes for each input, but then the class name and file name parameters would have to be vectors.
                    if (InputFiles.Length != 1)
                    {
                        Log.LogErrorWithCodeFromResources("ResGen.STRLanguageButNotExactlyOneSourceFile");
                        return false;
                    }
                }
                else
                {
                    if (
                        !String.IsNullOrEmpty(StronglyTypedClassName) ||
                        !String.IsNullOrEmpty(StronglyTypedNamespace) ||
                        !String.IsNullOrEmpty(StronglyTypedFileName)
                        )
                    {
                        // We have no language to generate a STR, but nevertheless the user passed us a class, 
                        // namespace, and/or filename. Let them know that they probably wanted to pass a language too.
                        Log.LogErrorWithCodeFromResources("ResGen.STRClassNamespaceOrFilenameWithoutLanguage");
                        return false;
                    }
                }

                // Verify that the ToolPath exists -- if the tool doesn't exist in it 
                // we'll worry about that later
                if ((String.IsNullOrEmpty(ToolPath) || !Directory.Exists(ToolPath)) &&
                    (String.IsNullOrEmpty(SdkToolsPath) || !Directory.Exists(SdkToolsPath)))
                {
                    Log.LogErrorWithCodeFromResources("ResGen.SdkOrToolPathNotSpecifiedOrInvalid", SdkToolsPath ?? "", ToolPath ?? "");
                    return false;
                }

                return base.ValidateParameters();
            }

            #endregion // ToolTask Members

            #region Helper methods

            /// <summary>
            /// Checks a string array for null or length zero.  Does not check if 
            /// individual members are null
            /// </summary>
            /// <param name="value">The string array to check</param>
            /// <returns>True if the array is null or has length zero</returns>
            private static bool IsNullOrEmpty(ITaskItem[] value)
            {
                return (value == null || value.Length == 0);
            }

            /// <summary>
            /// If OutputFiles is null, we need to generate default output names
            /// to pass to resgen.exe (which would generate the names on its own, but
            /// then we wouldn't have access to them)
            /// </summary>
            private void GenerateOutputFileNames()
            {
                ErrorUtilities.VerifyThrow(!IsNullOrEmpty(InputFiles), "If InputFiles is empty, the task should have returned before reaching this point");

                ITaskItem[] inputFiles = InputFiles;
                ITaskItem[] outputFiles = new ITaskItem[inputFiles.Length];

                // Set the default OutputFiles values
                for (int i = 0; i < inputFiles.Length; i++)
                {
                    if (inputFiles[i] is ITaskItem2 inputFileAsITaskItem2)
                    {
                        outputFiles[i] = new TaskItem(Path.ChangeExtension(inputFileAsITaskItem2.EvaluatedIncludeEscaped, ".resources"));
                    }
                    else
                    {
                        outputFiles[i] = new TaskItem(Path.ChangeExtension(EscapingUtilities.Escape(inputFiles[i].ItemSpec), ".resources"));
                    }
                }

                Bag[nameof(OutputFiles)] = outputFiles;
            }

            /// <summary>
            /// Generates the full path to ResGen.exe.  
            /// </summary>
            /// <returns>The path to ResGen.exe, or null.</returns>
            private string GenerateResGenFullPath()
            {
                // Use ToolPath if it exists.
                var pathToTool = (string)Bag["ToolPathWithFile"];
                if (pathToTool == null)
                {
                    // First see if the user has set ToolPath
                    if (ToolPath != null)
                    {
                        pathToTool = Path.Combine(ToolPath, ToolExe);

                        if (!File.Exists(pathToTool))
                        {
                            pathToTool = null;
                        }
                    }

                    // If it still hasn't been found, try to generate the appropriate path. 
                    if (pathToTool == null)
                    {
                        pathToTool = SdkToolsPathUtility.GeneratePathToTool
                                        (
                                            SdkToolsPathUtility.FileInfoExists,
                                            MSBuildProcessorArchitecture.CurrentProcessArchitecture,
                                            SdkToolsPath,
                                            ToolExe,
                                            Log,
                                            true /* log errors and warnings */
                                        );

                        pathToTool = NativeMethodsShared.GetLongFilePath(pathToTool);
                    }

                    // And then set it for future reference.  If it's still null, there's nothing else 
                    // we can do, and we've already logged an appropriate error. 
                    Bag["ToolPathWithFile"] = pathToTool;
                }

                return pathToTool;
            }

            /// <summary>
            /// Generate the command line to be passed to resgen.exe, sans the path to the tool. 
            /// </summary>
            private void GenerateResGenCommands(CommandLineBuilderExtension resGenArguments, bool useForResponseFile)
            {
                resGenArguments = resGenArguments ?? new CommandLineBuilderExtension();

                if (IsNullOrEmpty(OutputFiles))
                {
                    GenerateOutputFileNames();
                }

                // Append boolean flags if requested
                string useSourcePathSwitch = "/useSourcePath" + (useForResponseFile ? "\n" : String.Empty);
                string publicClassSwitch = "/publicClass" + (useForResponseFile ? "\n" : String.Empty);
                resGenArguments.AppendWhenTrue(useSourcePathSwitch, Bag, "UseSourcePath");
                resGenArguments.AppendWhenTrue(publicClassSwitch, Bag, "PublicClass");

                // append the references, if any
                if (References != null)
                {
                    foreach (ITaskItem reference in References)
                    {
                        // ResGen.exe response files frown on quotes in filenames, even if there are 
                        // spaces in the names of the files.  
                        if (useForResponseFile && reference != null)
                        {
                            resGenArguments.AppendTextUnquoted("/r:");
                            resGenArguments.AppendTextUnquoted(reference.ItemSpec);
                            resGenArguments.AppendTextUnquoted("\n");
                        }
                        else
                        {
                            resGenArguments.AppendSwitchIfNotNull("/r:", reference);
                        }
                    }
                }

                if (String.IsNullOrEmpty(StronglyTypedLanguage))
                {
                    // append the compile switch
                    resGenArguments.AppendSwitch("/compile" + (useForResponseFile ? "\n" : String.Empty));

                    // append the resources to compile
                    if (InputFiles != null && InputFiles.Length > 0)
                    {
                        ITaskItem[] inputFiles = InputFiles;
                        ITaskItem[] outputFiles = OutputFiles;

                        for (int i = 0; i < inputFiles.Length; ++i)
                        {
                            if (useForResponseFile)
                            {
                                // ResGen.exe response files frown on quotes in filenames, even if there are 
                                // spaces in the names of the files.  
                                if (inputFiles[i] != null && outputFiles[i] != null)
                                {
                                    resGenArguments.AppendTextUnquoted(inputFiles[i].ItemSpec);
                                    resGenArguments.AppendTextUnquoted(",");
                                    resGenArguments.AppendTextUnquoted(outputFiles[i].ItemSpec);
                                    resGenArguments.AppendTextUnquoted("\n");
                                }
                            }
                            else
                            {
                                resGenArguments.AppendFileNamesIfNotNull
                                (
                                    new[] { inputFiles[i], outputFiles[i] },
                                    ","
                                );
                            }
                        }
                    }
                }
                else
                {
                    // append the resource to compile
                    resGenArguments.AppendFileNamesIfNotNull(InputFiles, " ");
                    resGenArguments.AppendFileNamesIfNotNull(OutputFiles, " ");

                    // append the strongly-typed resource details
                    resGenArguments.AppendSwitchIfNotNull
                    (
                        "/str:",
                        new[] { StronglyTypedLanguage, StronglyTypedNamespace, StronglyTypedClassName, StronglyTypedFileName },
                        ","
                    );
                }
            }

            #endregion // Helper methods
        }
    }
}
