using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#if (OrganizationalAuth || IndividualB2CAuth)
using Microsoft.Extensions.Options;
#endif

namespace Company.WebApplication1
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
#if (OrganizationalAuth || IndividualB2CAuth)
            services.AddJwtBearerAuthentication();

#endif
#if (OrganizationalAuth)

            services.Configure<AzureAdOptions>(Configuration.GetSection("Authentication:AzureAd"));
#endif
#if (IndividualB2CAuth)

            services.Configure<AzureAdB2COptions>(Configuration.GetSection("Authentication:AzureAdB2C"));
#endif
#if (OrganizationalAuth || IndividualB2CAuth)
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();
#endif
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseMvc();
        }
    }
}
