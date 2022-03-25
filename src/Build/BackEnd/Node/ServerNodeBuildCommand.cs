// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains all of the information necessary for a entry node to run a command line.
    /// </summary>
    internal class ServerNodeBuildCommand : INodePacket
    {
        public ServerNodeBuildCommand(string commandLine, string startupDirectory, Dictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture)
        {
            CommandLine = commandLine;
            StartupDirectory = startupDirectory;
            BuildProcessEnvironment = buildProcessEnvironment;
            Culture = culture;
            UICulture = uiCulture;
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeBuildCommand()
        {
        }

        #region INodePacket Members

        /// <summary>
        /// Retrieves the packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeBuilCommand;

        #endregion

        /// <summary>
        /// The startup directory
        /// </summary>
        public string CommandLine { get; private set; } = default!;

        /// <summary>
        /// The startup directory
        /// </summary>
        public string StartupDirectory { get; private set; } = default!;

        /// <summary>
        /// The process environment.
        /// </summary>
        public Dictionary<string, string> BuildProcessEnvironment { get; private set; } = default!;

        /// <summary>
        /// The culture
        /// </summary>
        public CultureInfo Culture { get; private set; } = default!;

        /// <summary>
        /// The UI culture.
        /// </summary>
        public CultureInfo UICulture { get; private set; } = default!;

        #region INodePacketTranslatable Members

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                var br = translator.Reader;

                CommandLine = br.ReadString();
                StartupDirectory = br.ReadString();
                int count = br.ReadInt32();
                BuildProcessEnvironment = new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < count; ++i)
                {
                    var key = br.ReadString();
                    var value = br.ReadString();
                    BuildProcessEnvironment.Add(key, value);
                }
                Culture = new CultureInfo(br.ReadString());
                UICulture = new CultureInfo(br.ReadString());
            }
            else
            {
                throw new InvalidOperationException("Writing into stream not supported");
            }
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeBuildCommand command = new();
            command.Translate(translator);

            return command;
        }
        #endregion
    }
}
