using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenEvent.Web.Contexts;
using OpenEvent.Web.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace OpenEvent.Web
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
            // services.AddDbContext<ApplicationContext>(options =>
            //     options.UseSqlite(
            //     Configuration.GetConnectionString("DefaultConnection")));
            
            services.AddDbContext<ApplicationContext>(options =>
                options.UseMySql(Configuration.GetConnectionString("SQLConnection"),new MySqlServerVersion(new Version(8, 0, 23)), mySqlOptions => mySqlOptions .CharSetBehavior(CharSetBehavior.NeverAppend)));

            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddDefaultIdentity<User>()
                .AddEntityFrameworkStores<ApplicationContext>();

            services.AddIdentityServer()
                .AddApiAuthorization<User, ApplicationContext>();
            
            services.AddAuthentication()
                .AddIdentityServerJwt();

            services.AddControllersWithViews();
            // In production, the Angular files will be served from this directory
            services.AddSpaStaticFiles(configuration => { configuration.RootPath = "ClientApp/dist"; });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // app.UseHttpsRedirection();
            app.UseStaticFiles();
            if (!env.IsDevelopment())
            {
                app.UseSpaStaticFiles();
            }

            app.UseRouting();
            
            app.UseAuthentication();
            app.UseIdentityServer();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });

            // app.UseSpa(spa =>
            // {
            //     // To learn more about options for serving an Angular SPA from ASP.NET Core,
            //     // see https://go.microsoft.com/fwlink/?linkid=864501
            //
            //     spa.Options.SourcePath = "ClientApp";
            //
            //     if (env.IsDevelopment())
            //     {
            //         spa.UseAngularCliServer(npmScript: "start");
            //     }
            // });
        }
    }
}