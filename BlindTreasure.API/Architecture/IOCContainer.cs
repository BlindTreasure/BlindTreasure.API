using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Net.payOS;
using VaccinaCare.API.Resolvers;
using VaccinaCare.Application.Interface;
using VaccinaCare.Application.Interface.Common;
using VaccinaCare.Application.Interface.PaymentService;
using VaccinaCare.Application.Service;
using VaccinaCare.Application.Service.Common;
using VaccinaCare.Application.Service.ThirdParty;
using VaccinaCare.Domain;
using VaccinaCare.Repository;
using VaccinaCare.Repository.Commons;
using VaccinaCare.Repository.Interfaces;
using VaccinaCare.Repository.Repositories;

namespace VaccinaCare.API.Architechture;

public static class IOCContainer
{
    public static IServiceCollection SetupIOCContainer(this IServiceCollection services)
    {
        //Add Logger
        services.AddScoped<ILoggerService, LoggerService>();

        //Add Project Services
        services.SetupDBContext();
        services.SetupSwagger();

        //Add generic repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        //Add business services
        services.SetupBusinessServicesLayer();

        services.SetupCORS();
        services.SetupJWT();
        services.SetupGraphQl();
        services.SetupVnpay();
        services.SetupPayOs();
        return services;
    }



    public static IServiceCollection SetupGraphQl(this IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .AddErrorFilter<GraphQLErrorFilter>()
            .AddQueryType<Query>();
        
        return services;
    }
    
    public static IServiceCollection SetupPayOs(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        //PayOS
        services.AddSingleton<PayOS>(provider =>
        {
            var clientId = configuration["Payment:PayOS:ClientId"] ??
                           throw new Exception("Cannot find PAYOS_CLIENT_ID");
            var apiKey = configuration["Payment:PayOS:ApiKey"] ?? throw new Exception("Cannot find PAYOS_API_KEY");
            var checksumKey = configuration["Payment:PayOS:ChecksumKey"] ??
                              throw new Exception("Cannot find PAYOS_CHECKSUM_KEY");

            return new PayOS(clientId, apiKey, checksumKey);
        });
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

        services.AddDbContext<VaccinaCareDbContext>(options => 
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")), 
            ServiceLifetime.Scoped); 

        return services;
    }

    public static IServiceCollection SetupBusinessServicesLayer(this IServiceCollection services)
    {
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Add application services
        services.AddScoped<Query>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICurrentTime, CurrentTime>();
        services.AddScoped<IClaimsService, ClaimsService>();
        services.AddScoped<ILoggerService, LoggerService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IVaccineService, VaccineService>();
        services.AddScoped<IChildService, ChildService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IVaccineSuggestionService, VaccineSuggestionService>();
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IVaccinePackageService, VaccinePackageService>();
        services.AddScoped<IVaccineIntervalRulesService, VaccineIntervalRulesService>();
        services.AddScoped<IVaccineRecordService, VaccineRecordService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IVaccineRecordService, VaccineRecordService>();
        services.AddScoped<IPolicyService, PolicyService>();

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
                new OpenApiInfo { Title = "VaccinaCareAPI", Version = "v1" });
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
            options.AddPolicy("CustomerPolicy", policy =>
                policy.RequireRole("Customer"));

            options.AddPolicy("AdminPolicy", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("StaffPolicy", policy =>
                policy.RequireRole("Staff"));

            options.AddPolicy("AdminOrStaffPolicy", policy =>
                policy.RequireRole("Admin", "Staff"));

            options.AddPolicy("StaffOrCustomerPolicy", policy =>
                policy.RequireRole("Customer", "Staff"));
        });

        return services;
    }
}