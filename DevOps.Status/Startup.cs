using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OAuth.GitHub;
using DevOps.Status.Util;
using DevOps.Util;
using DevOps.Util.DotNet;
using DevOps.Util.DotNet.Function;
using DevOps.Util.DotNet.Triage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
                    options.Conventions.AuthorizeFolder("/Tracking", Constants.TriagePolicy);
                });
            services.AddControllers();

            services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = true;
                options.AppendTrailingSlash = true;
            });

            services.AddHttpContextAccessor();
            services.AddScoped<DotNetQueryUtilFactory>();
            services.AddScoped<IGitHubClientFactory>(_ => GitHubClientFactory.Create(Configuration));

            services.AddScoped(_ => new FunctionQueueUtil(Configuration[DotNetConstants.ConfigurationAzureBlobConnectionString]));

            services.AddDbContext<TriageContext>(options => 
            {
                var connectionString = Configuration.GetNonNull(DotNetConstants.ConfigurationSqlConnectionString);
#if DEBUG
                options.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
                options.EnableSensitiveDataLogging();
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
                // If we are going to impersonate the logged in user rather than use the GH app
                // then we need some additional permissions to manage GH issues
                if (Configuration[DotNetConstants.ConfigurationGitHubImpersonateUser] == "true")
                {
                    options.Scope.Add("public_repo");
                }
                options.ClientId = Configuration.GetNonNull(DotNetConstants.ConfigurationGitHubClientId);
                options.ClientSecret = Configuration.GetNonNull(DotNetConstants.ConfigurationGitHubClientSecret);
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
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    Constants.TriagePolicy,
                    policy => policy
                       .RequireRole(Constants.TriageRole)
                       .AddAuthenticationSchemes(GitHubAuthenticationDefaults.AuthenticationScheme));
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

            // At the time the ClientFactory was created we hadn't done authentication yet. Now that we have
            // associate the user's GH access token with the factory in case we need it to impersonate them.
            // This is only necessary when the app is configured to use the logged in user's identity rather
            // than the dedicated GH runfo app identity.
            app.Use(async (context, next) =>
            {
                if(context.User.Identity.IsAuthenticated)
                {
                    string accessToken = await context.GetTokenAsync("access_token");
                    IGitHubClientFactory gitHubClientFactory = context.RequestServices.GetService<IGitHubClientFactory>();
                    gitHubClientFactory.SetUserOAuthToken(accessToken);
                }
                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
