using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class NetworkHelper
    {
        // in milliseconds
        private const int Timeout = 50000;

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
                        Thread.Sleep(100);
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

        public static string GetLocalhostUrlWithFreePort()
        {
            return $"{Localhost}:{PortManager.GetPort()}/";
        }
    }
}
