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
using LogiTrack.Email;
using LogiTrack.Security;
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
                    "Set Jwt:Key through a secret provider with a signing key of at least 32 bytes.");
            }

            var issuer = Configuration["Jwt:Issuer"];
            var audience = Configuration["Jwt:Audience"];
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException("Configure Jwt:Issuer and Jwt:Audience.");
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            services.AddMemoryCache();
            services.AddScoped<LogiTrackContext>();
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.Password.RequiredLength = 12;
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedEmail = true;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.AllowedForNewUsers = true;
                })
                .AddEntityFrameworkStores<LogiTrackContext>()
                .AddDefaultTokenProviders();
            services.Configure<DataProtectionTokenProviderOptions>(options =>
                options.TokenLifespan = TimeSpan.FromHours(1));
            services.Configure<SmtpOptions>(Configuration.GetSection("Smtp"));
            services.AddTransient<IEmailSender, SmtpEmailSender>();
            services.AddScoped<AuthRateLimitFilter>();
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
            if (env.IsProduction())
            {
                ValidateProductionConfiguration();
            }

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

        private void ValidateProductionConfiguration()
        {
            var smtpHost = Configuration["Smtp:Host"];
            var fromEmail = Configuration["Smtp:FromEmail"];
            var smtpUserName = Configuration["Smtp:UserName"];
            var smtpPassword = Configuration["Smtp:Password"];
            var apiBaseUrl = Configuration["Auth:PublicApiBaseUrl"];

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException(
                    "Configure Smtp:Host and Smtp:FromEmail for production email delivery.");
            }

            if (string.IsNullOrWhiteSpace(smtpUserName) != string.IsNullOrWhiteSpace(smtpPassword))
            {
                throw new InvalidOperationException(
                    "Configure both Smtp:UserName and Smtp:Password, or neither.");
            }

            int smtpPort;
            if (!int.TryParse(Configuration["Smtp:Port"], out smtpPort) ||
                smtpPort < 1 ||
                smtpPort > 65535 ||
                smtpPort == 465)
            {
                throw new InvalidOperationException(
                    "Configure an SMTP STARTTLS port (typically 587); implicit TLS port 465 is not supported.");
            }

            Uri apiUri;
            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out apiUri) ||
                apiUri.Scheme != Uri.UriSchemeHttps ||
                apiUri.IsLoopback)
            {
                throw new InvalidOperationException(
                    "Configure Auth:PublicApiBaseUrl with the public HTTPS API URL.");
            }
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
