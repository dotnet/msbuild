// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Xml;
using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Unit tests for the AppConfig class
    /// </summary>
    public class AppConfig_Tests
    {
        /// <summary>
        /// A simple app.config.
        /// </summary>
        [Fact]
        public void Simple()
        {
            AppConfig app = new AppConfig();

            string xml =
                "<configuration>\n" +
                "    <runtime>\n" +
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='Simple' PublicKeyToken='b03f5f7f11d50a3a' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n" +
                "    </runtime>\n" +
                "</configuration>";

            app.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            string s = Summarize(app);

            Assert.Contains("Dependent Assembly: Simple, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a oldVersionLow=1.0.0.0 oldVersionHigh=1.0.0.0 newVersion=2.0.0.0", s);
        }

        /// <summary>
        /// A simple app.config.
        /// </summary>
        [Fact]
        public void SimpleRange()
        {
            AppConfig app = new AppConfig();

            string xml =
                "<configuration>\n" +
                "    <runtime>\n" +
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='Simple' PublicKeyToken='b03f5f7f11d50a3a' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n" +
                "    </runtime>\n" +
                "</configuration>";

            app.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            string s = Summarize(app);

            Assert.Contains("Dependent Assembly: Simple, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a oldVersionLow=1.0.0.0 oldVersionHigh=2.0.0.0 newVersion=2.0.0.0", s);
        }

        /// <summary>
        /// An app.config taken from rascal, that has some bindingRedirects.
        /// </summary>
        [Fact]
        public void RascalTest()
        {
            AppConfig app = new AppConfig();

            string xml =
                "<configuration>\n" +
                "    <runtime>\n" +
                "        <assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>\n" +
                "            <probing privatePath='PrimaryInteropAssemblies'/>\n" +
                "            <qualifyAssembly partialName='System.Web' fullName='System.Web, Version=1.2.3300.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a, Custom=null'/>\n" +
                "            <qualifyAssembly partialName='System' fullName='System, Version=1.2.3300.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, Custom=null'/>\n" +
                "            <qualifyAssembly partialName='CustomMarshalers' fullName='CustomMarshalers, Version=1.2.3300.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'/>\n" +
                "            <qualifyAssembly partialName='CustomMarshalers, Version=1.2.3300.0' fullName='CustomMarshalers, Version=1.2.3300.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'/>\n" +
                "            <qualifyAssembly partialName='CustomMarshalers, Version=1.2.3300.0, PublicKeyToken=b03f5f7f11d50a3a' fullName='CustomMarshalers, Version=1.2.3300.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'/>\n" +
                "        </assemblyBinding>\n" +
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='Microsoft.VSDesigner' PublicKeyToken='b03f5f7f11d50a3a' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='7.0.3300.0' newVersion='8.0.1000.0' />\n" +
                "        </dependentAssembly>\n" +
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='Microsoft.VisualStudio.Designer.Interfaces' PublicKeyToken='b03f5f7f11d50a3a' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.3300.0' newVersion='1.2.3400.0' />\n" +
                "        </dependentAssembly>\n" +
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='Microsoft.VisualStudio' PublicKeyToken='b03f5f7f11d50a3a' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.3300.0' newVersion='1.2.3400.0' />\n" +
                "        </dependentAssembly>\n" +
                "    </runtime>\n" +
                "    <system.net>\n" +
                "        <settings>\n" +
                "            <ipv6 enabled='true' />\n" +
                "        </settings>\n" +
                "    </system.net>\n" +
                "</configuration>";

            app.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            string s = Summarize(app);

            Assert.Contains("Dependent Assembly: Microsoft.VSDesigner, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a oldVersionLow=7.0.3300.0 oldVersionHigh=7.0.3300.0 newVersion=8.0.1000.0", s);
            Assert.Contains("Dependent Assembly: Microsoft.VisualStudio.Designer.Interfaces, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a oldVersionLow=1.0.3300.0 oldVersionHigh=1.0.3300.0 newVersion=1.2.3400.0", s);
            Assert.Contains("Dependent Assembly: Microsoft.VisualStudio, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a oldVersionLow=1.0.3300.0 oldVersionHigh=1.0.3300.0 newVersion=1.2.3400.0", s);
        }

        /// <summary>
        /// A machine.config file.
        /// </summary>
        [Fact]
        public void MachineConfig()
        {
            AppConfig app = new AppConfig();

            string xml =
                "<configuration>\n" +
                "    <runtime>\n" +
                "        <developerSettings\n" +
                "                           installationVersion='v2.0.40107.0' />\n" +
                "   <assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>\n" +
                "      <dependentAssembly>\n" +
                "        <assemblyIdentity name='Microsoft.VSDesigner' publicKeyToken='b03f5f7f11d50a3a' />\n" +
                "        <bindingRedirect oldVersion='7.1.3300.0' newVersion='7.2.3300.0' />\n" +
                "      </dependentAssembly>\n" +
                "    </assemblyBinding>\n" +
                "    </runtime>\n" +
                "    <system.runtime.remoting>\n" +
                "        <application>\n" +
                "            <channels>\n" +
                "                <channel ref='http client' displayName='http client (delay loaded)' delayLoadAsClientChannel='true' />\n" +
                "                <channel ref='tcp client' displayName='tcp client (delay loaded)' delayLoadAsClientChannel='true' />\n" +
                "                <channel ref='tcps client' displayName='tcps client (delay loaded)' delayLoadAsClientChannel='true' />\n" +
                "            </channels>\n" +
                "        </application>\n" +
                "        <channels>\n" +
                "            <channel id='http' type='System.Runtime.Remoting.Channels.Http.HttpChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='https' secure='true' type='System.Runtime.Remoting.Channels.Http.HttpChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='http client' type='System.Runtime.Remoting.Channels.Http.HttpClientChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='http server' type='System.Runtime.Remoting.Channels.Http.HttpServerChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='https server' secure='true' type='System.Runtime.Remoting.Channels.Http.HttpServerChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='tcp' type='System.Runtime.Remoting.Channels.Tcp.TcpChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='tcps' secure='true' type='System.Runtime.Remoting.Channels.Tcp.TcpChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='tcp client' type='System.Runtime.Remoting.Channels.Tcp.TcpClientChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='tcps client' secure='true' type='System.Runtime.Remoting.Channels.Tcp.TcpClientChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='tcp server' type='System.Runtime.Remoting.Channels.Tcp.TcpServerChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            <channel id='tcps server' secure='true' type='System.Runtime.Remoting.Channels.Tcp.TcpServerChannel, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "        </channels>\n" +
                "        <channelSinkProviders>\n" +
                "            <clientProviders>\n" +
                "                <formatter id='soap' type='System.Runtime.Remoting.Channels.SoapClientFormatterSinkProvider, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "                <formatter id='binary' type='System.Runtime.Remoting.Channels.BinaryClientFormatterSinkProvider, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            </clientProviders>\n" +
                "            <serverProviders>\n" +
                "                <formatter id='soap' type='System.Runtime.Remoting.Channels.SoapServerFormatterSinkProvider, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "                <formatter id='binary' type='System.Runtime.Remoting.Channels.BinaryServerFormatterSinkProvider, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "                <provider id='wsdl' type='System.Runtime.Remoting.MetadataServices.SdlChannelSinkProvider, System.Runtime.Remoting, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' />\n" +
                "            </serverProviders>\n" +
                "        </channelSinkProviders>\n" +
                "    </system.runtime.remoting>\n" +
                "</configuration>        ";

            app.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            string s = Summarize(app);

            Assert.Contains("Dependent Assembly: Microsoft.VSDesigner, Version=0.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a oldVersionLow=7.1.3300.0 oldVersionHigh=7.1.3300.0 newVersion=7.2.3300.0", s);
        }

        /// <summary>
        /// Make sure that only dependent assemblies under the configuration-->runtime tag work.
        /// </summary>
        [Fact]
        public void Regress339840_DependentAssemblyUnderAlienTag()
        {
            AppConfig app = new AppConfig();

            string xml =
                "<configuration>\n" +
                "    <runtime>\n" +
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='Simple' PublicKeyToken='b03f5f7f11d50a3a' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n" +
                "    </runtime>\n" +
                "</configuration>";

            app.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            string s = Summarize(app);

            Assert.Contains("Dependent Assembly", s);
        }



        /// <summary>
        /// Summarize the parsed contents of the app.config files.
        /// </summary>
        /// <param name="app"></param>
        private static string Summarize(AppConfig app)
        {
            StringBuilder b = new StringBuilder();

            foreach (DependentAssembly dependentAssembly in app.Runtime.DependentAssemblies)
            {
                foreach (BindingRedirect bindingRedirect in dependentAssembly.BindingRedirects)
                {
                    string message = String.Format("Dependent Assembly: {0} oldVersionLow={1} oldVersionHigh={2} newVersion={3}", dependentAssembly.PartialAssemblyName, bindingRedirect.OldVersionLow, bindingRedirect.OldVersionHigh, bindingRedirect.NewVersion);
                    b.AppendLine(message);
                }
            }
            Console.WriteLine(b.ToString());
            return b.ToString();
        }
    }
}
