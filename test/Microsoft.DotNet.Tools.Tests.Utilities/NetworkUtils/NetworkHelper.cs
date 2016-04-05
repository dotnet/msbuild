using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel;
using FluentAssertions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class NetworkHelper
    {
        // in milliseconds
        private const int Timeout = 20000;

        private static Queue<TcpListener> s_PortPool = new Queue<TcpListener>();

        public static string Localhost { get; } = "http://localhost";

        public static bool IsServerUp(string url)
        {
            return SpinWait.SpinUntil(() =>
            {
                using (var client = new HttpClient())
                {
                    try
                    {
                        client.BaseAddress = new Uri(url);
                        HttpResponseMessage response = client.GetAsync("").Result;
                        return response.IsSuccessStatusCode;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }, Timeout);
        }

        public static void TestGetRequest(string url, string expectedResponse)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(url);

                HttpResponseMessage response = client.GetAsync("").Result;
                if (response.IsSuccessStatusCode)
                {
                    var responseString = response.Content.ReadAsStringAsync().Result;
                    responseString.Should().Contain(expectedResponse);
                }
            }
        }

        public static int GetFreePort()
        {
            lock (s_PortPool)
            {
                if (s_PortPool.Count == 0)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        var tcpl = new TcpListener(IPAddress.Loopback, 0);
                        tcpl.Start();
                        s_PortPool.Enqueue(tcpl);
                    }

                    Console.WriteLine($"Ports Count >>>>>>>>>>>>>>>>>>> {s_PortPool.Count}");
                }

                var currentTcpl = s_PortPool.Dequeue();
                var port = ((IPEndPoint)currentTcpl.LocalEndpoint).Port;
                currentTcpl.Stop();
                currentTcpl = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();

                return port;
            }
        }

        public static string GetLocalhostUrlWithFreePort()
        {
            return $"{Localhost}:{GetFreePort()}/";
        }
    }
}
