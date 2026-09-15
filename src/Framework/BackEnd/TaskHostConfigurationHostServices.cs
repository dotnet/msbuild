// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Transport-only representation of the remote host objects needed by a task host.
    /// </summary>
    internal sealed class TaskHostConfigurationHostServices : ITranslatable
    {
        private List<HostObject> _hostObjects;

        public TaskHostConfigurationHostServices()
        {
        }

        internal IReadOnlyList<HostObject> HostObjects => _hostObjects;

        internal void Add(string projectFile, string targetName, string taskName, string monikerName)
        {
            _hostObjects ??= new List<HostObject>();
            _hostObjects.Add(new HostObject(projectFile, targetName, taskName, monikerName));
        }

        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                int count = translator.Reader.ReadInt32();
                _hostObjects = new List<HostObject>();

                for (int i = 0; i < count; i++)
                {
                    _hostObjects.Add(
                        new HostObject(
                            translator.Reader.ReadString(),
                            translator.Reader.ReadString(),
                            translator.Reader.ReadString(),
                            translator.Reader.ReadString()));
                }
            }
            else
            {
                int count = _hostObjects?.Count ?? 0;
                translator.Writer.Write(count);

                for (int i = 0; i < count; i++)
                {
                    HostObject hostObject = _hostObjects[i];
                    translator.Writer.Write(hostObject.ProjectFile);
                    translator.Writer.Write(hostObject.TargetName);
                    translator.Writer.Write(hostObject.TaskName);
                    translator.Writer.Write(hostObject.MonikerName);
                }
            }
        }

        internal readonly struct HostObject
        {
            internal HostObject(string projectFile, string targetName, string taskName, string monikerName)
            {
                ProjectFile = projectFile;
                TargetName = targetName;
                TaskName = taskName;
                MonikerName = monikerName;
            }

            internal string ProjectFile { get; }

            internal string TargetName { get; }

            internal string TaskName { get; }

            internal string MonikerName { get; }
        }
    }
}
