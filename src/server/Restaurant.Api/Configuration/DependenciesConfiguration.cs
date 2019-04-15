﻿using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Restaurant.Api.OperationFilters;
using Restaurant.Core.AuthContext;
using Restaurant.Domain.Entities;
using Restaurant.Persistence.EntityFramework;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Marten;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Restaurant.Api.Events;
using Restaurant.Business.AuthContext;
using Restaurant.Core.AuthContext.Configuration;
using Restaurant.Domain.Events._Base;

namespace Restaurant.Api.Configuration
{
    public static class DependenciesConfiguration
    {
        public static void AddDbContext(this IServiceCollection services, string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(nameof(connectionString));
            }

            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseNpgsql(connectionString)
                    .EnableSensitiveDataLogging());
        }

        public static void AddJwtIdentity(this IServiceCollection services, IConfigurationSection jwtConfiguration)
        {
            services.AddTransient<IJwtFactory, JwtFactory>();

            services.AddIdentity<User, IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            var signingKey = new SymmetricSecurityKey(
                Encoding.Default.GetBytes(jwtConfiguration["Secret"]));

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtConfiguration[nameof(JwtConfiguration.Issuer)],

                ValidateAudience = true,
                ValidAudience = jwtConfiguration[nameof(JwtConfiguration.Audience)],

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                RequireExpirationTime = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            services.Configure<JwtConfiguration>(options =>
            {
                options.Issuer = jwtConfiguration[nameof(JwtConfiguration.Issuer)];
                options.Audience = jwtConfiguration[nameof(JwtConfiguration.Audience)];
                options.SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(configureOptions =>
            {
                configureOptions.ClaimsIssuer = jwtConfiguration[nameof(JwtConfiguration.Issuer)];
                configureOptions.TokenValidationParameters = tokenValidationParameters;
                configureOptions.SaveToken = true;
            });

            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                options.AddPolicy(AuthConstants.Policies.IsAdmin, pb => pb.RequireClaim(AuthConstants.ClaimTypes.IsAdmin));
            });
        }

        public static void AddSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(setup =>
            {
                setup.SwaggerDoc("v1", new Info { Title = "Restaurant.Api", Version = "v1" });
                setup.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Restaurant.Api.Documentation.xml"));

                setup.AddSecurityDefinition("Bearer",
                    new ApiKeyScheme
                    {
                        In = "header",
                        Description = "Enter 'Bearer {token}' (don't forget to add 'bearer') into the field below.",
                        Name = "Authorization",
                        Type = "apiKey"
                    });

                setup.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>
                {
                    { "Bearer", Enumerable.Empty<string>() },
                });

                setup.OperationFilter<OptionOperationFilter>();
            });
        }

        public static void AddCqrs(this IServiceCollection services)
        {
            services.AddScoped<IEventBus, EventBus>();
        }

        public static void AddMapper(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg =>
            {
                cfg.AddProfiles(typeof(MappingProfile).Assembly);
            });
        }

        public static void AddCommonServices(this IServiceCollection services)
        {

        }

        public static void AddMarten(this IServiceCollection services, IConfiguration configuration)
        {
            var documentStore = DocumentStore.For(options =>
            {
                var config = configuration.GetSection("EventStore");
                var connectionString = config.GetValue<string>("ConnectionString");
                var schemaName = config.GetValue<string>("Schema");

                options.Connection(connectionString);
                options.AutoCreateSchemaObjects = AutoCreate.All;
                options.Events.DatabaseSchemaName = schemaName;
                options.DatabaseSchemaName = schemaName;

                //options.Events.InlineProjections.AggregateStreamsWith<Tab>();
                //options.Events.InlineProjections.Add(new TabViewProjection());

                //var events = typeof(TabOpened)
                //  .Assembly
                //  .GetTypes()
                //  .Where(t => typeof(IEvent).IsAssignableFrom(t))
                //  .ToList();

                //options.Events.AddEventTypes(events);
            });

            services.AddSingleton<IDocumentStore>(documentStore);

            services.AddScoped(sp => sp.GetService<IDocumentStore>().OpenSession());
        }
    }
}
