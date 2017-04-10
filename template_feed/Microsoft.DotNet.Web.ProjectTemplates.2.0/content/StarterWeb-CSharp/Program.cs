using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Company.WebApplication1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureConfiguration((context, configBuilder) => {
#if (IndividualAuth || OrganizationalAuth)
                    configBuilder
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

                    if (context.HostingEnvironment.IsDevelopment())
                    {
                        // For more details on using the user secret store see https://go.microsoft.com/fwlink/?LinkID=532709
                        builder.AddUserSecrets<Startup>();
                    }

                    configBuilder.AddEnvironmentVariables();
#else
                    configBuilder
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                        .AddEnvironmentVariables();
#endif
                })
                .ConfigureLogging(loggerFactory => loggerFactory
                    .AddConsole()
                    .AddDebug())
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}