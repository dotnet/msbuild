using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if (IndividualLocalAuth)
using Company.WebApplication1.Data;
using Company.WebApplication1.Models;
using Company.WebApplication1.Services;
#endif
#if (OrganizationalAuth || IndividualB2CAuth)
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
#endif
using Microsoft.AspNetCore.Builder;
#if (IndividualLocalAuth)
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
#endif
#if (OrganizationalAuth || IndividualAuth)
using Microsoft.AspNetCore.Http;
#endif
#if (IndividualAuth)
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.AspNetCore.Hosting;
#if (OrganizationalAuth || IndividualB2CAuth)
using Microsoft.AspNetCore.Authentication.Extensions;
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
            services.AddDbContext<ApplicationDbContext>(options =>
  #if (UseLocalDB)
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
  #else
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));
  #endif

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
#elseif (IndividualB2CAuth)
            services.AddAzureAdB2CAuthentication();
#elseif (OrganizationalAuth)
            services.AddAzureAdAuthentication();
#endif
#if (IndividualLocalAuth)

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
#elseif (OrganizationalAuth)

            services.AddAuthentication(
                SharedOptions => SharedOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme);
#endif

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
#if (UseBrowserLink)
                app.UseBrowserLink();
#endif
#if (IndividualLocalAuth)
                app.UseDatabaseErrorPage();
#endif
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

#if (OrganizationalAuth || IndividualAuth)
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