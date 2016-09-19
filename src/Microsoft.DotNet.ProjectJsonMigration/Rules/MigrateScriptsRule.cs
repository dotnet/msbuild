// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils.CommandParsing;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    public class MigrateScriptsRule : IMigrationRule
    {
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
            foreach (var scriptCommand in scriptCommands)
            {
                AddExec(target, FormatScriptCommand(scriptCommand));
            }

            return target;
        }

        internal string FormatScriptCommand(string scriptCommandline)
        {
            return ReplaceScriptVariables(scriptCommandline);
        }

        internal string ReplaceScriptVariables(string scriptCommandline)
        {
            Func<string, string> scriptVariableReplacementDelegate = key =>
            {
                if (ScriptVariableToMSBuildMap.ContainsKey(key))
                {
                    if (ScriptVariableToMSBuildMap[key] == null)
                    {
                        MigrationErrorCodes.MIGRATE1016(
                                $"{key} is currently an unsupported script variable for project migration")
                            .Throw();
                    }

                    return ScriptVariableToMSBuildMap[key];
                }
                return $"$({key})";
            };

            var scriptArguments = CommandGrammar.Process(
                scriptCommandline,
                scriptVariableReplacementDelegate,
                preserveSurroundingQuotes: true);

            scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();

            return string.Join(" ", scriptArguments);
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

            // Run Scripts After each inner build
            target.Condition = " '$(IsCrossTargetingBuild)' != 'true' ";

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

        private static Dictionary<string, string> ScriptVariableToMSBuildMap => new Dictionary<string, string>
        {
            { "compile:ResponseFile", null },     // Not migrated
            { "compile:CompilerExitCode", null }, // Not migrated
            { "compile:RuntimeOutputDir", null }, // Not migrated
            { "compile:RuntimeIdentifier", null },// Not Migrated
            
            { "compile:TargetFramework", "$(TargetFramework)" },
            { "publish:TargetFramework", "$(TargetFramework)" },
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
