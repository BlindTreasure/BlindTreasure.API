using System.Reflection;
using System.Security.Claims;
using System.Text;
using BlindTreasure.Application.GHTK.Authorization;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Services.Commons;
using BlindTreasure.Application.Services.ThirdParty.AIModels;
using BlindTreasure.Domain;
using BlindTreasure.Infrastructure;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using BlindTreasure.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Resend;
using StackExchange.Redis;

namespace BlindTreasure.API.Architecture;

public static class IocContainer
{
    public static IServiceCollection SetupIocContainer(this IServiceCollection services)
    {
        //Add Logger
        services.AddScoped<ILoggerService, LoggerService>();

        //Add Project Services
        services.SetupDbContext();
        services.SetupSwagger();

        //Add generic repositories
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        //Add business services
        services.AddScoped<IOAuthService, OAuthService>();
        services.SetupBusinessServicesLayer();

        services.SetupJwt();

        // services.SetupGraphQl();
        services.SetupReSendService();

        services.SetupVnpay();
        return services;
    }


    public static IServiceCollection SetupRedisService(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnectionString))
            throw new InvalidOperationException("Redis connection string is missing in environment variables.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<ICacheService, RedisCacheService>();

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

    public static IServiceCollection SetupReSendService(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddHttpClient<ResendClient>();
        services.Configure<ResendClientOptions>(o =>
        {
            o.ApiToken = Environment.GetEnvironmentVariable("RESEND_APITOKEN")!;
        });
        services.AddTransient<IResend, ResendClient>();

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

    private static IServiceCollection SetupDbContext(this IServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<BlindTreasureDbContext>(options =>
            options.UseNpgsql(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(BlindTreasureDbContext).Assembly.FullName);
                sql.CommandTimeout(300); // Cấu hình thời gian timeout truy vấn (tính bằng giây)
                sql.EnableRetryOnFailure(
                    5,
                    TimeSpan.FromSeconds(10),
                    null
                );
            })
        );

        return services;
    }


    public static IServiceCollection SetupBusinessServicesLayer(this IServiceCollection services)
    {
        // services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Add application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILoggerService, LoggerService>();
        services.AddScoped<IMapperService, MapperService>();
        services.AddScoped<ICurrentTime, CurrentTime>();
        services.AddScoped<IClaimsService, ClaimsService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISellerService, SellerService>();
        services.AddScoped<ISellerVerificationService, SellerVerificationService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IDataAnalyzerService, DataAnalyzerService>();
        services.AddScoped<IBlindBoxService, BlindBoxService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<ICartItemService, CartItemService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IStripeService, StripeService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IInventoryItemService, InventoryItemService>();
        services.AddScoped<ICustomerBlindBoxService, CustomerBlindBoxService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUnboxingService, UnboxingService>();
        services.AddScoped<IChatMessageService, ChatMessageService>();
        services.AddScoped<IGhnShippingService, GhnShippingService>();

        //3rd party
        services.AddHttpClient();
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<IBlindyService, BlindyService>();
        services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 102400; // 100 KB
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                options.StreamBufferCapacity = 10;
            })
            .AddJsonProtocol(options => { options.PayloadSerializerOptions.PropertyNamingPolicy = null; });


        services.AddHttpContextAccessor();

        return services;
    }


    public static IServiceCollection SetupSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "BlindTreasure API",
                Version = "v1",
                Description = "API cho hệ thống thương mại điện tử BlindTreasure."
            });

            c.UseInlineDefinitionsForEnums();
            c.UseAllOfForInheritance();

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhập token vào format: Bearer {your token}"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                    Array.Empty<string>()
                }
            });

            // Load XML comment
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });


        return services;
    }

    private static IServiceCollection SetupJwt(this IServiceCollection services)
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
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidIssuer = configuration["JWT:Issuer"],
                    ValidAudience = configuration["JWT:Audience"],
                    IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:SecretKey"] ??
                                                                        throw new InvalidOperationException())),
                    NameClaimType = ClaimTypes.NameIdentifier
                };
                x.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs/notification") ||
                             path.StartsWithSegments("/hubs/chat")))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            })
            .AddXClientSource(options =>
            {
                options.IssuerSigningKey = configuration["IssuerSigningKey"] ?? "";
                options.ClientValidator = async (clientSource, token, principle) => true;
            });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("CustomerPolicy", policy =>
                policy.RequireRole("Customer"));

            options.AddPolicy("AdminPolicy", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("StaffPolicy", policy =>
                policy.RequireRole("Staff"));

            options.AddPolicy("SellerPolicy", policy =>
                policy.RequireRole("Seller"));
        });

        return services;
    }
}