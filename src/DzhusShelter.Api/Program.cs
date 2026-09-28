using System.Text;
using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.Auth;
using DzhusShelter.Api.Application.Auth.Commands;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Application.BadHabits.Commands;
using DzhusShelter.Api.Application.BadHabits.Dtos;
using DzhusShelter.Api.Application.BadHabits.Queries;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure;
using DzhusShelter.Api.Infrastructure.Auth;
using DzhusShelter.Api.Infrastructure.BadHabits;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    })
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IHabitEntryRepository, EfHabitEntryRepository>();
builder.Services.AddScoped<IValidator<LogHabitEntryCommand>, LogHabitEntryCommandValidator>();
builder.Services.AddScoped<IValidator<GetHabitEntriesQuery>, GetHabitEntriesQueryValidator>();
builder.Services.AddScoped<ICommandHandler<LogHabitEntryCommand, Result<LogHabitEntryResult>>, LogHabitEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>, GetHabitEntriesQueryHandler>();

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IValidator<LoginCommand>, LoginCommandValidator>();
builder.Services.AddScoped<ICommandHandler<LoginCommand, Result<string>>, LoginCommandHandler>();

var app = builder.Build();

// Cloudflare Tunnel terminates TLS at its edge and forwards to this container over plain
// HTTP — without this, Request.IsHttps is always false and RemoteIpAddress is always the
// tunnel's own hop, breaking both secure-cookie logic (UI side) and the login rate
// limiter's per-IP partitioning above. Docker's port publishing means the immediate hop's
// IP isn't a fixed, known address, so the default trusted-proxy allowlist doesn't apply —
// clearing it is the standard fix for a containerized reverse-proxy setup like this one.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

public partial class Program;
