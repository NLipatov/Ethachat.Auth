using System.Text.Json;
using AspNetCore.Proxy;
using AuthAPI.Extensions;
using AuthAPI.Services.AuthenticationManager;
using AuthAPI.Services.AuthenticationManager.Implementations.Jwt;
using AuthAPI.Services.AuthenticationManager.Implementations.Jwt.Implementation;
using AuthAPI.Services.AuthenticationManager.Implementations.WebAuthn.Implementation;
using AuthAPI.Services.JWT.JwtReading;
using AuthAPI.Services.JWT.JwtReading.Implementation;
using AuthAPI.Services.RefreshHistoryService;
using AuthAPI.Services.RefreshHistoryService.Implementation;
using Ethachat.Auth.Infrastructure.Context;
using Ethachat.Auth.Infrastructure.DB.DBContext;
using EthachatShared.Models.Authentication.Models.Credentials.Implementation;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

var myAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddDbContext<AuthContext>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
        corsPolicyBuilder =>
        {
            corsPolicyBuilder
                .AllowAnyOrigin()
                .AllowAnyHeader();
        });
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Bearer Authentication With JWT",
        Type = SecuritySchemeType.Http
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme
                }
            },
            new List<string>()
        }
    });
});

builder.Services.AddProxies();
builder.Services.UseCryptographyHelper();
builder.Services.UseUserProvider();
builder.Services.UseUserCredentialsValidator();
builder.Services.AddAuthorization();
builder.Services.AddTransient<IJwtReader, JwtReader>();
builder.Services.AddTransient<IJwtAuthenticationManager, JwtAuthenticationManager>();
builder.Services.AddTransient<IJwtRefreshHistoryService, JwtRefreshHistoryService>();
builder.Services.AddTransient<IAuthenticationManager<JwtPair>, JwtAuthenticationManager>();
builder.Services.AddTransient<IAuthenticationManager<WebAuthnPair>, WebAuthnAuthenticationManager>();

// Use the in-memory implementation of IDistributedCache.
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    // Set a short timeout for easy testing.
    options.IdleTimeout = TimeSpan.FromMinutes(2);
    options.Cookie.HttpOnly = true;
    // Strict SameSite mode is required because the default mode used
    // by ASP.NET Core 3 isn't understood by the Conformance Tool
    // and breaks conformance testing
    options.Cookie.SameSite = SameSiteMode.None;
});

var envServerDomain = Environment.GetEnvironmentVariable("SERVER_DOMAIN");
var serverDomain = string.IsNullOrWhiteSpace(envServerDomain)
    ? builder.Configuration["fido2:serverDomain"]
    : envServerDomain;
Console.WriteLine($"FIDO2: Using server domain: {serverDomain}");

var envOrigins = JsonSerializer.Deserialize<HashSet<string>>(Environment.GetEnvironmentVariable("ORIGINS") ?? "[]");
var origins = envOrigins is not null && envOrigins.Any()
    ? envOrigins
    : builder.Configuration.GetSection("fido2:origins").Get<HashSet<string>>();
Console.WriteLine($"FIDO2: Using origins: {string.Join(" ,", origins ?? new())}");

builder.Services.AddFido2(options =>
{
    options.Origins = origins;
    options.ServerDomain = serverDomain;
    options.ServerName = "FIDO2 Test";
    options.TimestampDriftTolerance = builder.Configuration.GetValue<int>("fido2:timestampDriftTolerance");
    options.MDSCacheDirPath = builder.Configuration["fido2:MDSCacheDirPath"];
});

#region CORS setup

builder.Services.AddCors(p => p.AddPolicy("loose-CORS",
    corsPolicyBuilder => { corsPolicyBuilder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader(); }));

#endregion

var app = builder.Build();

await using (var applyMigrationsScope = app.Services.CreateAsyncScope())
{
    await applyMigrationsScope.ApplyPendingMigrationsAsync<AuthContext>();
}

app.UseCors(myAllowSpecificOrigins);
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("loose-CORS");

app.UseAuthorization();

app.MapControllers();

app.Run();