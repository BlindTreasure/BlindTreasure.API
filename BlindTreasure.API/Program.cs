using BlindTreasure.API.Architecture;
using BlindTreasure.Application.GHTK.Authorization;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.ThirdParty.AIModels;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Services.ThirdParty.AIModels;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.DTOs.StripeDTOs;
using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stripe;
using SwaggerThemes;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });
builder.Services.AddEndpointsApiExplorer();
builder.Services.SetupIocContainer();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddEnvironmentVariables(); // Cái này luôn phải nằm cuối

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder
                .WithOrigins(
                    "http://localhost:4040",
                    "https://blindtreasure.vercel.app"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetIsOriginAllowed(_ => true); // Allow WebSocket
        });
});

builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        options.SerializerSettings.Converters.Add(new StringEnumConverter());
    });


// Tắt việc map claim mặc định
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

#region STRIPE CONNECT SETUP

//Set Stripe API key
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
var appInfo = new AppInfo { Name = "BlindTreasure", Version = "v1" };
StripeConfiguration.AppInfo = appInfo;
builder.Services.AddHttpClient("Stripe");

builder.Services.AddTransient<IStripeClient, StripeClient>(s =>
{
    var clientFactory = s.GetRequiredService<IHttpClientFactory>();

    var sysHttpClient = new SystemNetHttpClient(
        clientFactory.CreateClient("Stripe"),
        StripeConfiguration.MaxNetworkRetries,
        appInfo,
        StripeConfiguration.EnableTelemetry);

    return new StripeClient(StripeConfiguration.ApiKey, httpClient: sysHttpClient);
});

#endregion

builder.Services.Configure<GhtkSettings>(builder.Configuration.GetSection("GHTK"));
builder.Services.AddHttpClient<IGhtkService, GhtkService>();

builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Services.AddEndpointsApiExplorer();
builder.Services.SetupRedisService(builder.Configuration);


var app = builder.Build();

app.UseCors("AllowFrontend");
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BlindTreasureAPI API v1");
        c.RoutePrefix = string.Empty;
        c.InjectStylesheet("/swagger-ui/custom-theme.css");
        c.HeadContent = $"<style>{SwaggerTheme.GetSwaggerThemeCss(Theme.OneDark)}</style>";
        c.ConfigObject.AdditionalItems.Add("persistAuthorization", "true");
        c.InjectJavascript("/custom-swagger.js");
        c.InjectStylesheet("/custom-swagger.css");
    });
}

try
{
    app.ApplyMigrations(app.Logger);
}
catch (Exception e)
{
    app.Logger.LogError(e, "An problem occurred during migration!");
}

//test thử middle ware này
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        // Format theo ApiResult
        var apiResult = new
        {
            isSuccess = false,
            isFailure = true,
            value = (object?)null,
            error = new
            {
                code = "500",
                message = "Đã xảy ra lỗi hệ thống.",
                detail = error?.Message
            }
        };

        var result = JsonSerializer.Serialize(apiResult);
        await context.Response.WriteAsync(result);
    });
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<ChatHub>("/hubs/chat");

app.UseStaticFiles();

app.Run();