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
using DzhusShelter.Api.Application.Birthdays;
using DzhusShelter.Api.Application.Birthdays.Commands;
using DzhusShelter.Api.Application.Birthdays.Dtos;
using DzhusShelter.Api.Application.Birthdays.Queries;
using DzhusShelter.Api.Infrastructure.BadHabits;
using DzhusShelter.Api.Infrastructure.Birthdays;
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

var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey))
    throw new InvalidOperationException("Jwt:SigningKey is not configured.");
if (Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes (256 bits) for HMACSHA256.");

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
    // Applies only to endpoints with no [Authorize]/[AllowAnonymous] of their own (Bad Habits,
    // ...): any JWT or API key — except the send-only main bot, whose key must be useless
    // outside its two notification endpoints (least privilege, docs/specs/birthdays.md).
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .RequireAssertion(context => !context.User.HasClaim(AuthPolicies.ClientNameClaim, AuthPolicies.MainBotClient))
        .Build();

    // Each named policy authenticates with its own scheme only, so a credential of the other
    // kind isn't even looked at — an API key can't reach UiOnly, a JWT can't reach the bot's.
    options.AddPolicy(AuthPolicies.UiOnly, policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());
    options.AddPolicy(AuthPolicies.MainBotNotifications, policy => policy
        .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
        .RequireClaim(AuthPolicies.ClientNameClaim, AuthPolicies.MainBotClient));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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

builder.Services.Configure<BirthdaysOptions>(builder.Configuration.GetSection(BirthdaysOptions.SectionName));
builder.Services.AddSingleton<IBirthdayClock, KyivBirthdayClock>();
builder.Services.AddScoped<IBirthdayRepository, EfBirthdayRepository>();
builder.Services.AddScoped<IBirthdayNotificationRepository, EfBirthdayNotificationRepository>();
builder.Services.AddScoped<IValidator<CreateBirthdayCommand>, CreateBirthdayCommandValidator>();
builder.Services.AddScoped<IValidator<UpdateBirthdayCommand>, UpdateBirthdayCommandValidator>();
builder.Services.AddScoped<IValidator<MarkBirthdayNotificationSentCommand>, MarkBirthdayNotificationSentCommandValidator>();
builder.Services.AddScoped<ICommandHandler<CreateBirthdayCommand, Result<Guid>>, CreateBirthdayCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateBirthdayCommand, Result>, UpdateBirthdayCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteBirthdayCommand, Result>, DeleteBirthdayCommandHandler>();
builder.Services.AddScoped<ICommandHandler<MarkBirthdayNotificationSentCommand, Result>, MarkBirthdayNotificationSentCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetBirthdaysQuery, Result<IReadOnlyList<BirthdayDto>>>, GetBirthdaysQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetPendingBirthdayNotificationsQuery, Result<IReadOnlyList<PendingBirthdayNotificationDto>>>, GetPendingBirthdayNotificationsQueryHandler>();

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
