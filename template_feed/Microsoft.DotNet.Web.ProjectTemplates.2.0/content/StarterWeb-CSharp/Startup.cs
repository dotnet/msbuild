using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if (OrganizationalAuth || IndividualB2CAuth)
using Microsoft.AspNetCore.Authentication.Cookies;
#endif
#if (MultiOrgAuth || IndividualB2CAuth)
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
#endif
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
#if (IndividualLocalAuth)
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#if (IndividualB2CAuth || OrganizationalAuth)
using Microsoft.Extensions.Options;
#endif
#if (OrganizationalAuth && OrgReadAccess)
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
#endif
#if (MultiOrgAuth)
using Microsoft.IdentityModel.Tokens;
#endif
#if (IndividualLocalAuth)
using Company.WebApplication1.Data;
using Company.WebApplication1.Models;
using Company.WebApplication1.Services;
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

#endif
            services.AddMvc();
#if (IndividualLocalAuth)

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
#endif
#if (OrganizationalAuth || IndividualB2CAuth)

            services.AddAuthentication(sharedOptions => 
            {
                sharedOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            });

#endif
#if (IndividualB2CAuth)

            services.Configure<AzureAdB2COptions>(Configuration.GetSection("Authentication:AzureAdB2C"));
#elseif (OrganizationalAuth)

            services.Configure<AzureAdOptions>(Configuration.GetSection("Authentication:AzureAd"));
#endif
#if (IndividualB2CAuth || OrganizationalAuth)
            services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, OpenIdConnectOptionsSetup>();
#endif

#if (IndividualLocalAuth)

            // Add external authentication handlers below. To configure them please see https://go.microsoft.com/fwlink/?LinkID=532715

#elseif (OrganizationalAuth || IndividualB2CAuth)
            services.AddCookieAuthentication();

            services.AddOpenIdConnectAuthentication();
            
#endif

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
#if (IndividualLocalAuth)
                app.UseDatabaseErrorPage();
#endif
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

#if (IndividualLocalAuth || OrganizationalAuth || IndividualB2CAuth)
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
