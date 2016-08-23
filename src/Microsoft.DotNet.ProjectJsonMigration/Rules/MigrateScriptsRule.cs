// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils.CommandParsing;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateScriptsRule : IMigrationRule
    {
        private static readonly string s_unixScriptExtension = ".sh";
        private static readonly string s_windowsScriptExtension = ".cmd";

        private readonly ITransformApplicator _transformApplicator;

        public MigrateScriptsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var scripts = projectContext.ProjectFile.Scripts;

            foreach (var scriptSet in scripts)
            {
                MigrateScriptSet(csproj, migrationRuleInputs.CommonPropertyGroup, scriptSet.Value, scriptSet.Key);
            }
        }

        public ProjectTargetElement MigrateScriptSet(ProjectRootElement csproj,
            ProjectPropertyGroupElement propertyGroup,
            IEnumerable<string> scriptCommands,
            string scriptSetName)
        {
            var target = CreateTarget(csproj, scriptSetName);
            var count = 0;
            foreach (var scriptCommand in scriptCommands)
            {
                var scriptExtensionPropertyName = AddScriptExtension(propertyGroup, scriptCommand, $"{scriptSetName}_{++count}");
                AddExec(target, FormatScriptCommand(scriptCommand, scriptExtensionPropertyName));
            }

            return target;
        }

        private string AddScriptExtension(ProjectPropertyGroupElement propertyGroup, string scriptCommandline, string scriptId)
        {
            var scriptArguments = CommandGrammar.Process(
                scriptCommandline,
                (s) => null,
                preserveSurroundingQuotes: false);

            scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();
            var scriptCommand = scriptArguments.First();
            var propertyName = $"MigratedScriptExtension_{scriptId}";

            var windowsScriptExtensionProperty = propertyGroup.AddProperty(propertyName,
                s_windowsScriptExtension);
            var unixScriptExtensionProperty = propertyGroup.AddProperty(propertyName,
                s_unixScriptExtension);

            windowsScriptExtensionProperty.Condition =
                $" '$(OS)' == 'Windows_NT' and Exists('{scriptCommand}{s_windowsScriptExtension}') ";
            unixScriptExtensionProperty.Condition =
                $" '$(OS)' != 'Windows_NT' and Exists('{scriptCommand}{s_unixScriptExtension}') ";

            return propertyName;
        }

        internal string FormatScriptCommand(string scriptCommandline, string scriptExtensionPropertyName)
        {
            var command = AddScriptExtensionPropertyToCommandLine(scriptCommandline, scriptExtensionPropertyName);
            return ReplaceScriptVariables(command);
        }

        internal string AddScriptExtensionPropertyToCommandLine(string scriptCommandline,
            string scriptExtensionPropertyName)
        {
            var scriptArguments = CommandGrammar.Process(
                scriptCommandline,
                (s) => null,
                preserveSurroundingQuotes: true);

            scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();

            var scriptCommand = scriptArguments.First();
            var trimmedCommand = scriptCommand.Trim('\"').Trim('\'');

            // Path.IsPathRooted only looks at paths conforming to the current os,
            // we need to account for all things
            if (!IsPathRootedForAnyOS(trimmedCommand))
            {
                scriptCommand = @".\" + scriptCommand;
            }

            if (scriptCommand.EndsWith("\"") || scriptCommand.EndsWith("'"))
            {
                var endChar = scriptCommand.Last();
                scriptCommand = $"{scriptCommand.TrimEnd(endChar)}$({scriptExtensionPropertyName}){endChar}";
            }
            else
            {
                scriptCommand += $"$({scriptExtensionPropertyName})";
            }

            var command = string.Join(" ", new[] {scriptCommand}.Concat(scriptArguments.Skip(1)));
            return command;
        }

        internal string ReplaceScriptVariables(string command)
        {
            foreach (var scriptVariableEntry in ScriptVariableToMSBuildMap)
            {
                var scriptVariableName = scriptVariableEntry.Key;
                var msbuildMapping = scriptVariableEntry.Value;

                if (command.Contains($"%{scriptVariableName}%"))
                {
                    if (msbuildMapping == null)
                    {
                        MigrationErrorCodes.MIGRATE1016(
                                $"{scriptVariableName} is currently an unsupported script variable for project migration")
                            .Throw();
                    }

                    command = command.Replace($"%{scriptVariableName}%", msbuildMapping);
                }
            }

            return command;
        }

        private bool IsPathRootedForAnyOS(string path)
        {
            return path.StartsWith("/") || path.Substring(1).StartsWith(":\\");
        }

        private ProjectTargetElement CreateTarget(ProjectRootElement csproj, string scriptSetName)
        {
            var targetName = $"{scriptSetName[0].ToString().ToUpper()}{string.Concat(scriptSetName.Skip(1))}Script";
            var targetHookInfo = ScriptSetToMSBuildHookTargetMap[scriptSetName];

            var target = csproj.CreateTargetElement(targetName);
            csproj.InsertBeforeChild(target, csproj.LastChild);
            if (targetHookInfo.IsRunBefore)
            {
                target.BeforeTargets = targetHookInfo.TargetName;
            }
            else
            {
                target.AfterTargets = targetHookInfo.TargetName;
            }

            return target;
        }

        private void AddExec(ProjectTargetElement target, string command)
        {
            var task = target.AddTask("Exec");
            task.SetParameter("Command", command);
        }

        // ProjectJson Script Set Name to 
        private static Dictionary<string, TargetHookInfo> ScriptSetToMSBuildHookTargetMap => new Dictionary<string, TargetHookInfo>()
        {
            { "precompile",  new TargetHookInfo(true, "Build") },
            { "postcompile", new TargetHookInfo(false, "Build") },
            { "prepublish",  new TargetHookInfo(true, "Publish") },
            { "postpublish", new TargetHookInfo(false, "Publish") }
        };

        private static Dictionary<string, string> ScriptVariableToMSBuildMap => new Dictionary<string, string>()
        {
            { "compile:TargetFramework", null },  // TODO: Need Short framework name in CSProj
            { "compile:ResponseFile", null },     // Not migrated
            { "compile:CompilerExitCode", null }, // Not migrated
            { "compile:RuntimeOutputDir", null }, // Not migrated
            { "compile:RuntimeIdentifier", null },// Not Migrated
            
            { "publish:TargetFramework", null },  // TODO: Need Short framework name in CSProj
            { "publish:Runtime", "$(RuntimeIdentifier)" },

            { "compile:FullTargetFramework", "$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)" },
            { "compile:Configuration", "$(Configuration)" },
            { "compile:OutputFile", "$(TargetPath)" },
            { "compile:OutputDir", "$(TargetDir)" },

            { "publish:ProjectPath", "$(MSBuildThisFileDirectory)" },
            { "publish:Configuration", "$(Configuration)" },
            { "publish:OutputPath", "$(TargetDir)" },
            { "publish:FullTargetFramework", "$(TargetFrameworkIdentifier),Version=$(TargetFrameworkVersion)" },

            { "project:Directory", "$(MSBuildProjectDirectory)" },
            { "project:Name", "$(AssemblyName)" },
            { "project:Version", "$(Version)" }
        };

        private class TargetHookInfo
        {
            public bool IsRunBefore { get; }
            public string TargetName { get; }

            public TargetHookInfo(bool isRunBefore, string targetName)
            {
                IsRunBefore = isRunBefore;
                TargetName = targetName;
            }
        }
    }
}
