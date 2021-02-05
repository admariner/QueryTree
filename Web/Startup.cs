﻿using System;
using Hangfire;
using Hangfire.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QueryTree.Models;
using QueryTree.Managers;
using QueryTree.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.HttpOverrides;

namespace QueryTree
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
            services.AddSingleton<IConfiguration>(Configuration);

            services.Configure<CustomizationConfiguration>(Configuration.GetSection("Customization"));
            services.Configure<PasswordsConfiguration>(Configuration.GetSection("Passwords"));

            switch (Configuration.GetValue<Enums.DataStoreType>("Customization:DataStore"))
            {
                case Enums.DataStoreType.MSSqlServer:
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
                    services.AddHangfire(x =>
                        x.UseSqlServerStorage(Configuration.GetConnectionString("DefaultConnection"))
                    );
                    break;

                default:
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));
                    services.AddHangfire(x =>
                        x.UseSQLiteStorage(Configuration.GetConnectionString("DefaultConnection"))
                    );
                    break;
            }

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            
            services.AddMvc(options => options.EnableEndpointRouting = false);
            
            services.AddAuthentication()
                .AddCookie(options => 
                {                
                    // Cookie settings
                    options.ExpireTimeSpan = TimeSpan.FromDays(150);
                    options.LoginPath = "/Account/LogIn";
                    options.LogoutPath = "/Account/LogOut";                    
                });
           
            services.Configure<IdentityOptions>(options =>
            {
                // Password settings
                options.Password.RequiredLength = 8;
                
                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 10;

                // User settings
                options.User.RequireUniqueEmail = true;
            });

            // Add application services.
            services.AddTransient<IEmailSenderService, EmailSenderService>();
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IPasswordManager, PasswordManager>(); // Allows controllers to set/get/delete database credentials
            services.AddTransient<IScheduledEmailManager, ScheduledEmailManager>();
			services.AddMemoryCache();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            LoggerFactory.Create(builder => builder.AddConsole());
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            // Enable use behind a reverse proxy
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseStaticFiles();

            app.UseAuthentication();

            if (Configuration["RunHangfire"] == "true")
            {
                app.UseHangfireServer();

                var dashboardOptions = new DashboardOptions
                {
                    Authorization = new[] { new HangfireAuthorizationFilter() }
                };
                app.UseHangfireDashboard("/hangfire", dashboardOptions);
            }

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
               
            if (!String.IsNullOrWhiteSpace(Configuration.GetValue<string>("Customization:BaseUri"))) {
                app.Use((context, next) => {
                    context.Request.PathBase = new PathString(Configuration.GetValue<string>("Customization:BaseUri"));
                    return next();
                });
            }
        }
    }
}
