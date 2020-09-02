using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OAuth.GitHub;
using AspNet.Security.OAuth.VisualStudio;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.Triage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;

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
                    options.Conventions.AuthorizePage("/Triage/New", Constants.TriagePolicy);
                    options.Conventions.AuthorizePage("/Search/BuildLogs", Constants.VsoPolicy);
                    options.Conventions.AuthorizePage("/Search/HelixLogs", Constants.VsoPolicy);

                });
            services.AddControllers();

            services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
                options.AppendTrailingSlash = true;
            });

            services.AddHttpContextAccessor();
            services.AddSingleton<StatusGitHubClientFactory>();
            services.AddScoped<DotNetQueryUtilFactory>();

            services.AddScoped<BlobStorageUtil>(_ =>
            {
                return new BlobStorageUtil(
                    DotNetUtil.AzureOrganization,
                    Configuration[DotNetConstants.ConfigurationAzureBlobConnectionString]);
            });

            services.AddDbContext<TriageContext>(options => 
            {
                var connectionString = Configuration[DotNetConstants.ConfigurationSqlConnectionString];
#if DEBUG
                options.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
#endif
                options.UseSqlServer(connectionString);
            });
            services.AddScoped<TriageContextUtil>();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = GitHubAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/signin";
                options.LogoutPath = "/signout";
            })
            .AddGitHub(options =>
            {
                options.ClientId = Configuration[DotNetConstants.ConfigurationGitHubClientId];
                options.ClientSecret = Configuration[DotNetConstants.ConfigurationGitHubClientSecret];
                options.SaveTokens = true;

                options.ClaimActions.MapJsonKey(Constants.GitHubAvatarUrl, Constants.GitHubAvatarUrl);
                options.Events.OnCreatingTicket = async context =>
                {
                    var userName = context.Identity.Name.ToLower();
                    var gitHubClient = GitHubClientFactory.CreateForToken(context.AccessToken, AuthenticationType.Oauth);
                    var organizations = await gitHubClient.Organization.GetAllForUser(userName);
                    var microsoftOrg = organizations.FirstOrDefault(x => x.Login.ToLower() == "microsoft");
                    if (microsoftOrg is object)
                    {
                        context.Identity.AddClaim(new Claim(context.Identity.RoleClaimType, Constants.TriageRole));
                    }
                };
            })
            .AddVisualStudio(options =>
            {
                options.ClientId = Configuration[DotNetConstants.ConfigurationVsoClientId];
                options.ClientSecret = Configuration[DotNetConstants.ConfigurationVsoClientSecret];
                options.SaveTokens = true;

                options.Events.OnCreatingTicket = context =>
                {
                    context.Identity.AddClaim(new Claim(context.Identity.RoleClaimType, Constants.VsoRole));
                    return Task.CompletedTask;
                };

                options.Scope.Add("vso.build_execute");
                options.Scope.Add("vso.identity");
                options.Scope.Add("vso.test_write");
                options.Scope.Add("vso.work");
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    Constants.TriagePolicy,
                    policy => policy
                       .RequireRole(Constants.TriageRole)
                       .AddAuthenticationSchemes(GitHubAuthenticationDefaults.AuthenticationScheme));
                options.AddPolicy(
                    Constants.VsoPolicy,
                    policy => policy
                       .RequireRole(Constants.VsoRole)
                       .AddAuthenticationSchemes(VisualStudioAuthenticationDefaults.AuthenticationScheme));
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
