using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DevOps.Util;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevOps.Status
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
            services
                .AddRazorPages()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AuthorizePage("/triage/new", Constants.TriagePolicy);

                });
            services.AddControllers();

            services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
                options.AppendTrailingSlash = true;
            });

            services.AddScoped<DevOpsServer>(_ =>
            {
                var token = Configuration["RUNFO_AZURE_TOKEN"];
                return new DevOpsServer("dnceng", token);
            });

            services.AddDbContext<TriageContext>(options => 
            {
                var connectionString = Configuration["RUNFO_CONNECTION_STRING"];
                options.UseSqlServer(connectionString);
            });

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/signin";
                options.LogoutPath = "/signout";
            })
            .AddGitHub(options =>
            {
                options.ClientId = Configuration["GitHubClientId"];
                options.ClientSecret = Configuration["GitHubClientSecret"];
                options.SaveTokens = true;
                options.ClaimActions.MapJsonKey(Constants.GitHubAvatarUrl, Constants.GitHubAvatarUrl);
                options.Events.OnCreatingTicket = context =>
                {
                    switch (context.Identity.Name.ToLower())
                    {
                        case "jaredpar":
                        case "pilchie":
                            context.Identity.AddClaim(new Claim(context.Identity.RoleClaimType, Constants.TriageRole));
                            break;
                    }

                    return Task.CompletedTask;
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Constants.TriagePolicy, policy => policy.RequireRole(Constants.TriageRole));
            });
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

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
