// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ProjectModel.Server
{
    public class ProjectModelServerCommand
    {
        private readonly Dictionary<int, ProjectManager> _projects;
        private readonly DesignTimeWorkspace _workspaceContext;
        private readonly ProtocolManager _protocolManager;
        private readonly string _hostName;
        private readonly int _port;
        private Socket _listenSocket;

        public ProjectModelServerCommand(int port, string hostName)
        {
            _port = port;
            _hostName = hostName;
            _protocolManager = new ProtocolManager(maxVersion: 4);
            _workspaceContext = new DesignTimeWorkspace(ProjectReaderSettings.ReadFromEnvironment());
            _projects = new Dictionary<int, ProjectManager>();
        }

        public static int Run(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "dotnet-projectmodel-server";
            app.Description = ".NET Project Model Server";
            app.FullName = ".NET Design Time Server";
            app.Description = ".NET Design Time Server";
            app.HelpOption("-?|-h|--help");

            var verbose = app.Option("--verbose", "Verbose ouput", CommandOptionType.NoValue);
            var hostpid = app.Option("--host-pid", "The process id of the host", CommandOptionType.SingleValue);
            var hostname = app.Option("--host-name", "The name of the host", CommandOptionType.SingleValue);
            var port = app.Option("--port", "The TCP port used for communication", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                try
                {
                    if (!MonitorHostProcess(hostpid))
                    {
                        return 1;
                    }

                    var intPort = CheckPort(port);
                    if (intPort == -1)
                    {
                        return 1;
                    }

                    if (!hostname.HasValue())
                    {
                        Reporter.Error.WriteLine($"Option \"{hostname.LongName}\" is missing.");
                        return 1;
                    }

                    var program = new ProjectModelServerCommand(intPort, hostname.Value());
                    program.OpenChannel();
                }
                catch (Exception ex)
                {
                    Reporter.Error.WriteLine($"Unhandled exception in server main: {ex}");
                    throw;
                }

                return 0;
            });

            return app.Execute(args);
        }

        public void OpenChannel()
        {
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, _port));
            _listenSocket.Listen(10);

            Reporter.Output.WriteLine($"Process ID {Process.GetCurrentProcess().Id}");
            Reporter.Output.WriteLine($"Listening on port {_port}");

            while (true)
            {
                var acceptSocket = _listenSocket.Accept();
                Reporter.Output.WriteLine($"Client accepted {acceptSocket.LocalEndPoint}");

                var connection = new ConnectionContext(acceptSocket,
                                                       _hostName,
                                                       _protocolManager,
                                                       _workspaceContext,
                                                       _projects);

                connection.QueueStart();
            }
        }

        public void Shutdown()
        {
            if (_listenSocket.Connected)
            {
                _listenSocket.Shutdown(SocketShutdown.Both);
            }
        }

        private static int CheckPort(CommandOption port)
        {
            if (!port.HasValue())
            {
                Reporter.Error.WriteLine($"Option \"{port.LongName}\" is missing.");
            }

            int result;
            if (int.TryParse(port.Value(), out result))
            {
                return result;
            }
            else
            {
                Reporter.Error.WriteLine($"Option \"{port.LongName}\" is not a valid Int32 value.");
                return -1;
            }
        }

        private static bool MonitorHostProcess(CommandOption host)
        {
            if (!host.HasValue())
            {
                Console.Error.WriteLine($"Option \"{host.LongName}\" is missing.");
                return false;
            }

            int hostPID;
            if (int.TryParse(host.Value(), out hostPID))
            {
                var hostProcess = Process.GetProcessById(hostPID);
                hostProcess.EnableRaisingEvents = true;
                hostProcess.Exited += (s, e) =>
                {
                    Process.GetCurrentProcess().Kill();
                };

                Reporter.Output.WriteLine($"Server will exit when process {hostPID} exits.");
                return true;
            }
            else
            {
                Reporter.Error.WriteLine($"Option \"{host.LongName}\" is not a valid Int32 value.");
                return false;
            }
        }
    }
}
