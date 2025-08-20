// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks
{
    using System;
    using System.Globalization;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Build.Utilities;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Tasks.Xaml;

    public partial class MASM : XamlDataDrivenToolTask
    {

        public MASM() :
                base(new string[] {
                            @"Inputs",
                            @"NoLogo",
                            @"GeneratePreprocessedSourceListing",
                            @"ListAllAvailableInformation",
                            @"UseSafeExceptionHandlers",
                            @"AddFirstPassListing",
                            @"EnableAssemblyGeneratedCodeListing",
                            @"DisableSymbolTable",
                            @"EnableFalseConditionalsInListing",
                            @"TreatWarningsAsErrors",
                            @"MakeAllSymbolsPublic",
                            @"GenerateDebugInformation",
                            @"EnableMASM51Compatibility",
                            @"PerformSyntaxCheckOnly",
                            @"ObjectFileName",
                            @"PreprocessorDefinitions",
                            @"AssembledCodeListingFile",
                            @"IncludePaths",
                            @"BrowseFile",
                            @"PreserveIdentifierCase",
                            @"WarningLevel",
                            @"PackAlignmentBoundary",
                            @"CallingConvention",
                            @"ErrorReporting",
                            @"CommandLineTemplate",
                            @"MASMBeforeTargets",
                            @"MASMAfterTargets",
                            @"ExecutionDescription",
                            @"AdditionalDependencies",
                            @"AdditionalOptions"}, new System.Resources.ResourceManager(@"", System.Reflection.Assembly.GetExecutingAssembly()))
        {
        }

        protected override string ToolName
        {
            get
            {
                return @"";
            }
        }

        [Required()]
        public virtual ITaskItem[] Inputs
        {
            get
            {
                if (this.IsPropertySet(@"Inputs"))
                {
                    return ActiveToolSwitches[@"Inputs"].TaskItemArray;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.ITaskItemArray);
                switchToAdd.DisplayName = @"Inputs";
                switchToAdd.Required = true;
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.Name = @"Inputs";
                switchToAdd.TaskItemArray = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool NoLogo
        {
            get
            {
                if (this.IsPropertySet(@"NoLogo"))
                {
                    return ActiveToolSwitches[@"NoLogo"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Suppress Startup Banner";
                switchToAdd.Description = @"Suppress the display of the startup banner and information messages.     (/nologo)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/nologo";
                switchToAdd.Name = @"NoLogo";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool GeneratePreprocessedSourceListing
        {
            get
            {
                if (this.IsPropertySet(@"GeneratePreprocessedSourceListing"))
                {
                    return ActiveToolSwitches[@"GeneratePreprocessedSourceListing"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Generate Preprocessed Source Listing";
                switchToAdd.Description = @"Generates a preprocessed source listing to the Output Window.     (/EP)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/EP";
                switchToAdd.Name = @"GeneratePreprocessedSourceListing";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool ListAllAvailableInformation
        {
            get
            {
                if (this.IsPropertySet(@"ListAllAvailableInformation"))
                {
                    return ActiveToolSwitches[@"ListAllAvailableInformation"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"List All Available Information";
                switchToAdd.Description = @"Turns on listing of all available information.     (/Sa)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Sa";
                switchToAdd.Name = @"ListAllAvailableInformation";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool UseSafeExceptionHandlers
        {
            get
            {
                if (this.IsPropertySet(@"UseSafeExceptionHandlers"))
                {
                    return ActiveToolSwitches[@"UseSafeExceptionHandlers"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Use Safe Exception Handlers";
                switchToAdd.Description = @"Marks the object as either containing no exception handlers or containing exception handlers that are all declared with .SAFESEH.     (/safeseh)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/safeseh";
                switchToAdd.Name = @"UseSafeExceptionHandlers";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool AddFirstPassListing
        {
            get
            {
                if (this.IsPropertySet(@"AddFirstPassListing"))
                {
                    return ActiveToolSwitches[@"AddFirstPassListing"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Add First Pass Listing";
                switchToAdd.Description = @"Adds first-pass listing to listing file.     (/Sf)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Sf";
                switchToAdd.Name = @"AddFirstPassListing";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool EnableAssemblyGeneratedCodeListing
        {
            get
            {
                if (this.IsPropertySet(@"EnableAssemblyGeneratedCodeListing"))
                {
                    return ActiveToolSwitches[@"EnableAssemblyGeneratedCodeListing"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Enable Assembly Generated Code Listing";
                switchToAdd.Description = @"Turns on listing of assembly-generated code.     (/Sg)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Sg";
                switchToAdd.Name = @"EnableAssemblyGeneratedCodeListing";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool DisableSymbolTable
        {
            get
            {
                if (this.IsPropertySet(@"DisableSymbolTable"))
                {
                    return ActiveToolSwitches[@"DisableSymbolTable"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Disable Symbol Table";
                switchToAdd.Description = @"Turns off symbol table when producing a listing.     (/Sn)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Sn";
                switchToAdd.Name = @"DisableSymbolTable";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool EnableFalseConditionalsInListing
        {
            get
            {
                if (this.IsPropertySet(@"EnableFalseConditionalsInListing"))
                {
                    return ActiveToolSwitches[@"EnableFalseConditionalsInListing"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Enable False Conditionals In Listing";
                switchToAdd.Description = @"Turns on false conditionals in listing.     (/Sx)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Sx";
                switchToAdd.Name = @"EnableFalseConditionalsInListing";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool TreatWarningsAsErrors
        {
            get
            {
                if (this.IsPropertySet(@"TreatWarningsAsErrors"))
                {
                    return ActiveToolSwitches[@"TreatWarningsAsErrors"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Treat Warnings As Errors";
                switchToAdd.Description = @"Returns an error code if warnings are generated.     (/WX)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/WX";
                switchToAdd.Name = @"TreatWarningsAsErrors";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool MakeAllSymbolsPublic
        {
            get
            {
                if (this.IsPropertySet(@"MakeAllSymbolsPublic"))
                {
                    return ActiveToolSwitches[@"MakeAllSymbolsPublic"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Make All Symbols Public";
                switchToAdd.Description = @"Makes all symbols public.     (/Zf)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Zf";
                switchToAdd.Name = @"MakeAllSymbolsPublic";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool GenerateDebugInformation
        {
            get
            {
                if (this.IsPropertySet(@"GenerateDebugInformation"))
                {
                    return ActiveToolSwitches[@"GenerateDebugInformation"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Generate Debug Information";
                switchToAdd.Description = @"Generates Debug Information.     (/Zi)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Zi";
                switchToAdd.Name = @"GenerateDebugInformation";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool EnableMASM51Compatibility
        {
            get
            {
                if (this.IsPropertySet(@"EnableMASM51Compatibility"))
                {
                    return ActiveToolSwitches[@"EnableMASM51Compatibility"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Enable MASM 5.1 Compatibility";
                switchToAdd.Description = @"Enables M510 option for maximum compatibility with MASM 5.1.     (/Zm)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Zm";
                switchToAdd.Name = @"EnableMASM51Compatibility";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual bool PerformSyntaxCheckOnly
        {
            get
            {
                if (this.IsPropertySet(@"PerformSyntaxCheckOnly"))
                {
                    return ActiveToolSwitches[@"PerformSyntaxCheckOnly"].BooleanValue;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.Boolean);
                switchToAdd.DisplayName = @"Perform Syntax Check Only";
                switchToAdd.Description = @"Performs a syntax check only.     (/Zs)";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/Zs";
                switchToAdd.Name = @"PerformSyntaxCheckOnly";
                switchToAdd.BooleanValue = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string ObjectFileName
        {
            get
            {
                if (this.IsPropertySet(@"ObjectFileName"))
                {
                    return ActiveToolSwitches[@"ObjectFileName"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Object File Name";
                switchToAdd.Description = @"Specifies the name of the output object file.     (/Fo:[file])";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.Name = @"ObjectFileName";
                switchToAdd.SwitchValue = @"/Fo""[value]""";
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string[] PreprocessorDefinitions
        {
            get
            {
                if (this.IsPropertySet(@"PreprocessorDefinitions"))
                {
                    return ActiveToolSwitches[@"PreprocessorDefinitions"].StringList;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.StringArray);
                switchToAdd.DisplayName = @"Preprocessor Definitions";
                switchToAdd.Description = @"Defines a text macro with the given name.     (/D[symbol])";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/D""[value]""";
                switchToAdd.Name = @"PreprocessorDefinitions";
                switchToAdd.StringList = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string AssembledCodeListingFile
        {
            get
            {
                if (this.IsPropertySet(@"AssembledCodeListingFile"))
                {
                    return ActiveToolSwitches[@"AssembledCodeListingFile"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Assembled Code Listing File";
                switchToAdd.Description = @"Generates an assembled code listing file.     (/Fl[file])";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.Name = @"AssembledCodeListingFile";
                switchToAdd.SwitchValue = @"/Fl""[value]""";
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string[] IncludePaths
        {
            get
            {
                if (this.IsPropertySet(@"IncludePaths"))
                {
                    return ActiveToolSwitches[@"IncludePaths"].StringList;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.StringArray);
                switchToAdd.DisplayName = @"Include Paths";
                switchToAdd.Description = @"Sets path for include file. A maximum of 10 /I options is allowed.     (/I [path])";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/I ""[value]""";
                switchToAdd.Name = @"IncludePaths";
                switchToAdd.StringList = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string[] BrowseFile
        {
            get
            {
                if (this.IsPropertySet(@"BrowseFile"))
                {
                    return ActiveToolSwitches[@"BrowseFile"].StringList;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.StringArray);
                switchToAdd.DisplayName = @"Generate Browse Information File";
                switchToAdd.Description = @"Specifies whether to generate browse information file and its optional name or location of the browse information file.     (/FR[name])";
                switchToAdd.IncludeInCommandLine = true;
                switchToAdd.SwitchValue = @"/FR""[value]""";
                switchToAdd.Name = @"BrowseFile";
                switchToAdd.StringList = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string PreserveIdentifierCase
        {
            get
            {
                if (this.IsPropertySet(@"PreserveIdentifierCase"))
                {
                    return ActiveToolSwitches[@"PreserveIdentifierCase"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Preserve Identifier Case";
                switchToAdd.Description = @"Specifies preservation of case of user identifiers.     (/Cp, /Cu, /Cx)";
                switchToAdd.IncludeInCommandLine = true;
                Tuple<string, string, Tuple<string, bool>[]>[] switchMap = new Tuple<string, string, Tuple<string, bool>[]>[] {
                        new Tuple<string, string, Tuple<string, bool>[]>(@"0", @"", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"1", @"/Cp", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"2", @"/Cu", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"3", @"/Cx", new Tuple<string, bool>[0])};
                int i = this.ReadSwitchMap2(@"PreserveIdentifierCase", switchMap, value);
                if (i >= 0)
                {
                    switchToAdd.SwitchValue = switchMap[i].Item2;
                    switchToAdd.Arguments = switchMap[i].Item3;
                }
                else
                {
                    switchToAdd.SwitchValue = "";
                    switchToAdd.Arguments = null;
                }
                switchToAdd.Separator = @"";
                switchToAdd.Name = @"PreserveIdentifierCase";
                switchToAdd.AllowMultipleValues = true;
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string WarningLevel
        {
            get
            {
                if (this.IsPropertySet(@"WarningLevel"))
                {
                    return ActiveToolSwitches[@"WarningLevel"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Warning Level";
                switchToAdd.Description = @"Sets the warning level, where level = 0, 1, 2, or 3.    (/W0, /W1, /W2, /W3)";
                switchToAdd.IncludeInCommandLine = true;
                Tuple<string, string, Tuple<string, bool>[]>[] switchMap = new Tuple<string, string, Tuple<string, bool>[]>[] {
                        new Tuple<string, string, Tuple<string, bool>[]>(@"0", @"/W0", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"1", @"/W1", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"2", @"/W2", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"3", @"/W3", new Tuple<string, bool>[0])};
                int i = this.ReadSwitchMap2(@"WarningLevel", switchMap, value);
                if (i >= 0)
                {
                    switchToAdd.SwitchValue = switchMap[i].Item2;
                    switchToAdd.Arguments = switchMap[i].Item3;
                }
                else
                {
                    switchToAdd.SwitchValue = "";
                    switchToAdd.Arguments = null;
                }
                switchToAdd.Separator = @"";
                switchToAdd.Name = @"WarningLevel";
                switchToAdd.AllowMultipleValues = true;
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string PackAlignmentBoundary
        {
            get
            {
                if (this.IsPropertySet(@"PackAlignmentBoundary"))
                {
                    return ActiveToolSwitches[@"PackAlignmentBoundary"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Pack Alignment Boundary";
                switchToAdd.Description = @"Packs structures on the specified byte boundary. The alignment can be 1, 2, 4, 8 or 16.     (/Zp1, /Zp2, /Zp4, /Zp8, /Zp16)";
                switchToAdd.IncludeInCommandLine = true;
                Tuple<string, string, Tuple<string, bool>[]>[] switchMap = new Tuple<string, string, Tuple<string, bool>[]>[] {
                        new Tuple<string, string, Tuple<string, bool>[]>(@"0", @"", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"1", @"/Zp1", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"2", @"/Zp2", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"3", @"/Zp4", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"4", @"/Zp8", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"5", @"/Zp16", new Tuple<string, bool>[0])};
                int i = this.ReadSwitchMap2(@"PackAlignmentBoundary", switchMap, value);
                if (i >= 0)
                {
                    switchToAdd.SwitchValue = switchMap[i].Item2;
                    switchToAdd.Arguments = switchMap[i].Item3;
                }
                else
                {
                    switchToAdd.SwitchValue = "";
                    switchToAdd.Arguments = null;
                }
                switchToAdd.Separator = @"";
                switchToAdd.Name = @"PackAlignmentBoundary";
                switchToAdd.AllowMultipleValues = true;
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string CallingConvention
        {
            get
            {
                if (this.IsPropertySet(@"CallingConvention"))
                {
                    return ActiveToolSwitches[@"CallingConvention"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Calling Convention";
                switchToAdd.Description = @"Selects calling convention for your application.     (/Gc, /Gd. /Gz)";
                switchToAdd.IncludeInCommandLine = true;
                Tuple<string, string, Tuple<string, bool>[]>[] switchMap = new Tuple<string, string, Tuple<string, bool>[]>[] {
                        new Tuple<string, string, Tuple<string, bool>[]>(@"0", @"", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"1", @"/Gd", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"2", @"/Gz", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"3", @"/Gc", new Tuple<string, bool>[0])};
                int i = this.ReadSwitchMap2(@"CallingConvention", switchMap, value);
                if (i >= 0)
                {
                    switchToAdd.SwitchValue = switchMap[i].Item2;
                    switchToAdd.Arguments = switchMap[i].Item3;
                }
                else
                {
                    switchToAdd.SwitchValue = "";
                    switchToAdd.Arguments = null;
                }
                switchToAdd.Separator = @"";
                switchToAdd.Name = @"CallingConvention";
                switchToAdd.AllowMultipleValues = true;
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string ErrorReporting
        {
            get
            {
                if (this.IsPropertySet(@"ErrorReporting"))
                {
                    return ActiveToolSwitches[@"ErrorReporting"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Error Reporting";
                switchToAdd.Description = @"Reports internal assembler errors to Microsoft.     (/errorReport:[method])";
                switchToAdd.IncludeInCommandLine = true;
                Tuple<string, string, Tuple<string, bool>[]>[] switchMap = new Tuple<string, string, Tuple<string, bool>[]>[] {
                        new Tuple<string, string, Tuple<string, bool>[]>(@"0", @"/errorReport:prompt", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"1", @"/errorReport:queue", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"2", @"/errorReport:send", new Tuple<string, bool>[0]),
                        new Tuple<string, string, Tuple<string, bool>[]>(@"3", @"/errorReport:none", new Tuple<string, bool>[0])};
                int i = this.ReadSwitchMap2(@"ErrorReporting", switchMap, value);
                if (i >= 0)
                {
                    switchToAdd.SwitchValue = switchMap[i].Item2;
                    switchToAdd.Arguments = switchMap[i].Item3;
                }
                else
                {
                    switchToAdd.SwitchValue = "";
                    switchToAdd.Arguments = null;
                }
                switchToAdd.Separator = @"";
                switchToAdd.Name = @"ErrorReporting";
                switchToAdd.AllowMultipleValues = true;
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string MASMBeforeTargets
        {
            get
            {
                if (this.IsPropertySet(@"MASMBeforeTargets"))
                {
                    return ActiveToolSwitches[@"MASMBeforeTargets"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Execute Before";
                switchToAdd.Description = @"Specifies the targets for the build customization to run before.";
                switchToAdd.Name = @"MASMBeforeTargets";
                switchToAdd.SwitchValue = @"";
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string MASMAfterTargets
        {
            get
            {
                if (this.IsPropertySet(@"MASMAfterTargets"))
                {
                    return ActiveToolSwitches[@"MASMAfterTargets"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Execute After";
                switchToAdd.Description = @"Specifies the targets for the build customization to run after.";
                switchToAdd.Name = @"MASMAfterTargets";
                switchToAdd.SwitchValue = @"";
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string ExecutionDescription
        {
            get
            {
                if (this.IsPropertySet(@"ExecutionDescription"))
                {
                    return ActiveToolSwitches[@"ExecutionDescription"].Value;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.String);
                switchToAdd.DisplayName = @"Execution Description";
                switchToAdd.Name = @"ExecutionDescription";
                switchToAdd.SwitchValue = @"";
                switchToAdd.Value = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }

        public virtual string[] AdditionalDependencies
        {
            get
            {
                if (this.IsPropertySet(@"AdditionalDependencies"))
                {
                    return ActiveToolSwitches[@"AdditionalDependencies"].StringList;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                CommandLineToolSwitch switchToAdd = new CommandLineToolSwitch(CommandLineToolSwitchType.StringArray);
                switchToAdd.DisplayName = @"Additional Dependencies";
                switchToAdd.Name = @"AdditionalDependencies";
                switchToAdd.StringList = value;
                this.ReplaceToolSwitch(switchToAdd);
            }
        }
    }
}
