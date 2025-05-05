using System.Text;
using BlindTreasure.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace BlindTreasure.API.Architecture;

public static class IOCContainer
{
    public static IServiceCollection SetupIOCContainer(this IServiceCollection services)
    {
        //Add Logger
        // services.AddScoped<ILoggerService, LoggerService>();

        //Add Project Services
        services.SetupDBContext();
        services.SetupSwagger();

        //Add generic repositories
        // services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        //Add business services
        services.SetupBusinessServicesLayer();

        services.SetupCORS();
        services.SetupJWT();
        // services.SetupGraphQl();
        services.SetupVnpay();
        services.SetupPayOs();
        return services;
    }


    // public static IServiceCollection SetupGraphQl(this IServiceCollection services)
    // {
    //     services
    //         .AddGraphQLServer()
    //         .AddErrorFilter<GraphQLErrorFilter>()
    //         .AddQueryType<Query>();
    //     
    //     return services;
    // }

    public static IServiceCollection SetupPayOs(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        //PayOS
        return services;
    }

    public static IServiceCollection SetupVnpay(this IServiceCollection services)
    {
        // Xây dựng IConfiguration từ các nguồn cấu hình
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Lấy thư mục hiện tại
            .AddJsonFile("appsettings.json", true, true) // Đọc appsettings.json
            .AddEnvironmentVariables() // Đọc biến môi trường từ Docker
            .Build();

        return services;
    }

    private static IServiceCollection SetupDBContext(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();


        // 2. Lấy connection string tên "DefaultConnection"
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // 3. Đăng ký DbContext với Npgsql
        services.AddDbContext<BlindTreasureDbContext>(options =>
            options.UseNpgsql(connectionString,
                sql => sql.MigrationsAssembly(typeof(BlindTreasureDbContext).Assembly.FullName)
            )
        );

        return services;
    }

    public static IServiceCollection SetupBusinessServicesLayer(this IServiceCollection services)
    {
        // services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Add application services


        services.AddHttpContextAccessor();

        return services;
    }

    private static IServiceCollection SetupCORS(this IServiceCollection services)
    {
        services.AddCors(opt =>
        {
            opt.AddPolicy("CorsPolicy",
                policy => { policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod(); });
        });

        return services;
    }

    private static IServiceCollection SetupSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.UseInlineDefinitionsForEnums();

            c.SwaggerDoc("v1",
                new OpenApiInfo { Title = "BlindTreasureAPI", Version = "v1" });
            var jwtSecurityScheme = new OpenApiSecurityScheme
            {
                Name = "JWT Authentication",
                Description = "Enter your JWT token in this field",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

            c.AddSecurityDefinition("Bearer", jwtSecurityScheme);

            var securityRequirement = new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            };

            c.AddSecurityRequirement(securityRequirement);

            // Cấu hình Swagger để sử dụng Newtonsoft.Json
            c.UseAllOfForInheritance();
        });

        return services;
    }

    private static IServiceCollection SetupJWT(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, // Bật kiểm tra Issuer
                    ValidateAudience = true, // Bật kiểm tra Audience
                    ValidateLifetime = true,
                    ValidIssuer = configuration["JWT:Issuer"],
                    ValidAudience = configuration["JWT:Audience"],
                    IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:SecretKey"]))
                };
            });
        services.AddAuthorization(options =>
        {
            // options.AddPolicy("CustomerPolicy", policy =>
            //     policy.RequireRole("Customer"));
            //
            // options.AddPolicy("AdminPolicy", policy =>
            //     policy.RequireRole("Admin"));
            //
            // options.AddPolicy("StaffPolicy", policy =>
            //     policy.RequireRole("Staff"));
            //
            // options.AddPolicy("AdminOrStaffPolicy", policy =>
            //     policy.RequireRole("Admin", "Staff"));
            //
            // options.AddPolicy("StaffOrCustomerPolicy", policy =>
            //     policy.RequireRole("Customer", "Staff"));
        });

        return services;
    }
}