using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.AspNetCore.Authentication.Extensions;
#endif
#if (OrganizationalAuth || IndividualB2CAuth)
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
#endif
using Microsoft.AspNetCore.Builder;
#if (IndividualLocalAuth)
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.Identity.Service;
#endif
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.AspNetCore.Hosting;
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.AspNetCore.Rewrite;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.Extensions.Options;
#endif
#if (OrganizationalAuth && OrgReadAccess)
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
#endif
#if (MultiOrgAuth)
using Microsoft.IdentityModel.Tokens;
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
#if (IndividualLocalAuth)
            services.AddIdentityServiceAuthentication();
#elseif (IndividualB2CAuth)
            services.AddAzureAdB2CAuthentication();
#elseif (OrganizationalAuth)
            services.AddAzureAdAuthentication();
#endif
#if (OrganizationalAuth || IndividualAuth)

#endif
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
#if (IndividualLocalAuth)
                app.UseDatabaseErrorPage();
                app.UseDevelopmentCertificateErrorPage(Configuration);
#endif
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

#if (OrganizationalAuth || IndividualAuth)
            app.UseRewriter(new RewriteOptions().AddIISUrlRewrite(env.ContentRootFileProvider, "urlRewrite.config"));

            app.UseAuthentication();

#endif
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
