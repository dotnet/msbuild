using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.AspNetCore.Authentication.Extensions;
#endif
using Microsoft.AspNetCore.Builder;
#if (IndividualLocalAuth)
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.Identity.Service;
#endif
using Microsoft.AspNetCore.Hosting;
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.AspNetCore.Rewrite;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
#if (IndividualLocalAuth)
            services.AddIdentityServiceAuthentication();
#elseif (IndividualB2CAuth)
            services.AddAzureAdB2CWebApiAuthentication();
#elseif (OrganizationalAuth)
            services.AddAzureAdWebApiAuthentication();
#endif
#if (OrganizationalAuth || IndividualAuth)

#endif
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
#if (IndividualLocalAuth)
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseDevelopmentCertificateErrorPage(Configuration);
            }

            app.UseStaticFiles();

#endif
#if (OrganizationalAuth || IndividualAuth)
            app.UseRewriter(new RewriteOptions().AddIISUrlRewrite(env.ContentRootFileProvider, "urlRewrite.config"));

            app.UseAuthentication();

#endif
            app.UseMvc();
        }
    }
}
