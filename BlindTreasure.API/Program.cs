using System.IdentityModel.Tokens.Jwt;
using BlindTreasure.API.Architecture;
using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;
using SwaggerThemes;
using JsonSerializer = System.Text.Json.JsonSerializer;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.SetupIocContainer();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddEnvironmentVariables(); // Cái này luôn phải nằm cuối

builder.Configuration.AddJsonFile("appsettings.json", true, true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        hehe =>
        {
            hehe.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});


builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    });


// Tắt việc map claim mặc định
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Services.AddEndpointsApiExplorer();
builder.Services.SetupRedisService(builder.Configuration);

var app = builder.Build();

app.UseCors("AllowAll");
//
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BlindTreasureAPI API v1");
        c.RoutePrefix = string.Empty;
        c.InjectStylesheet("/swagger-ui/custom-theme.css");
        c.HeadContent = $"<style>{SwaggerTheme.GetSwaggerThemeCss(Theme.OneDark)}</style>";
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
                // Có thể bổ sung detail = error?.Message nếu muốn debug, nhưng production nên bỏ
                detail = error?.Message
            }
        };

        var result = JsonSerializer.Serialize(apiResult);
        await context.Response.WriteAsync(result);
    });
});

// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();