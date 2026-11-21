using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogiTrack.Models;

namespace LogiTrack
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
            var jwtKey = Configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            {
                throw new InvalidOperationException(
                    "Configure Jwt:Key with a signing key of at least 32 bytes.");
            }

            var issuer = Configuration["Jwt:Issuer"];
            var audience = Configuration["Jwt:Audience"];
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException("Configure Jwt:Issuer and Jwt:Audience.");
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            services.AddScoped<LogiTrackContext>();
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<LogiTrackContext>()
                .AddDefaultTokenProviders();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey,
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILogger<Startup> logger)
        {
            SeedManagerRoleAsync(app.ApplicationServices, logger).GetAwaiter().GetResult();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseMvc();
        }

        private async Task SeedManagerRoleAsync(IServiceProvider services, ILogger<Startup> logger)
        {
            using (var scope = services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                const string managerRole = "Manager";

                if (!await roleManager.RoleExistsAsync(managerRole))
                {
                    var createRoleResult = await roleManager.CreateAsync(new IdentityRole(managerRole));
                    if (!createRoleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            "Unable to create Manager role: " +
                            string.Join(", ", createRoleResult.Errors.Select(error => error.Description)));
                    }
                }

                var configuredEmails = Configuration["Authorization:ManagerEmails"];
                if (string.IsNullOrWhiteSpace(configuredEmails))
                {
                    logger.LogInformation("No manager email addresses are configured.");
                    return;
                }

                var managerEmails = configuredEmails
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(email => email.Trim())
                    .Where(email => email.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var email in managerEmails)
                {
                    var user = await userManager.FindByEmailAsync(email);
                    if (user == null)
                    {
                        logger.LogWarning("Configured manager account {Email} was not found.", email);
                        continue;
                    }

                    if (!user.EmailConfirmed)
                    {
                        logger.LogWarning(
                            "Configured manager account {Email} is not email-confirmed; Manager role was not assigned.",
                            email);
                        continue;
                    }

                    if (!await userManager.IsInRoleAsync(user, managerRole))
                    {
                        var addToRoleResult = await userManager.AddToRoleAsync(user, managerRole);
                        if (!addToRoleResult.Succeeded)
                        {
                            throw new InvalidOperationException(
                                "Unable to assign Manager role: " +
                                string.Join(", ", addToRoleResult.Errors.Select(error => error.Description)));
                        }
                    }
                }
            }
        }
    }
}
