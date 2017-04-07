﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Diagnostics;
using BL.Auth;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Bearer.Web {
    public class Startup {
        public Startup(IHostingEnvironment env) {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            if (env.IsEnvironment("Development")) {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            services.AddApplicationInsightsTelemetry(Configuration);
            services.AddCors();
            // Enable the use of an [Authorize("Bearer")] attribute on methods and classes to protect.
            services.AddAuthorization(auth => {
                auth.AddPolicy(
                    "Bearer"
                , new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme‌​)
                    .RequireAuthenticatedUser().Build()
                );
            });

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory) {
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().AllowCredentials());
            //app.UseCors(builder => builder.WithOrigins("http://example.com"));


            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseApplicationInsightsRequestTelemetry();

            app.UseApplicationInsightsExceptionTelemetry();


            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else {
                app.UseExceptionHandler("/Home/Error");
            }

            #region Handle Exception
            app.UseExceptionHandler(appBuilder => {
                appBuilder.Use(async (context, next) => {
                    var error = context.Features[typeof(IExceptionHandlerFeature)] as IExceptionHandlerFeature;

                    //when authorization has failed, should return a json message to client
                    if (error != null && error.Error is SecurityTokenExpiredException) {
                        context.Response.StatusCode     = 401;
                        context.Response.ContentType    = "application/json";

                        await context.Response.WriteAsync(
                            JsonConvert.SerializeObject(new { authenticated = false, tokenExpired = true })
                        );
                    }
                    //when orther error, retrun a error message json to client
                    else if (error != null && error.Error != null) {
                        context.Response.StatusCode  = 500;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(
                            JsonConvert.SerializeObject( new { success = false, error = error.Error.Message } )
                        );
                    }
                    //when no error, do next.
                    else
                        await next();
                });
            });
            #endregion

            #region UseJwtBearerAuthentication
            var options = new JwtBearerOptions();
            options.TokenValidationParameters.IssuerSigningKey = TokenAuthOption.Key;
            options.TokenValidationParameters.ValidAudience    = TokenAuthOption.Audience;
            options.TokenValidationParameters.ValidIssuer      = TokenAuthOption.Issuer;

            // When receiving a token, check that we've signed it.
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;

            // When receiving a token, check that it is still valid.
            options.TokenValidationParameters.ValidateLifetime = true;

            // This defines the maximum allowable clock skew - i.e. provides a tolerance on the token expiry time 
            // when validating the lifetime. As we're creating the tokens locally and validating them on the same 
            // machines which should have synchronised time, this can be set to zero. Where external tokens are
            // used, some leeway here could be useful.
            options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(0);

            app.UseJwtBearerAuthentication(options);
            #endregion


            app.UseStaticFiles();

            app.UseMvc(routes => {
                routes.MapRoute(
                    name: "default"
                ,   template: "{controller=Login}/{action=Index}/{id?}"
                );
                //routes.MapRoute(
                //    name: "api"
                //, template: "api/{controller=TokenAuth}/{action=GetAuthToken}/{id?}"
                //);
            });
        }
    }
}
