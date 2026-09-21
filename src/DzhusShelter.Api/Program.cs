using System.Text.Json.Serialization;
using DzhusShelter.Api.Application.Abstractions;
using DzhusShelter.Api.Application.BadHabits;
using DzhusShelter.Api.Domain.Common;
using DzhusShelter.Api.Infrastructure;
using DzhusShelter.Api.Infrastructure.BadHabits;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IHabitEntryRepository, EfHabitEntryRepository>();
builder.Services.AddScoped<IValidator<LogHabitEntryCommand>, LogHabitEntryCommandValidator>();
builder.Services.AddScoped<IValidator<GetHabitEntriesQuery>, GetHabitEntriesQueryValidator>();
builder.Services.AddScoped<ICommandHandler<LogHabitEntryCommand, Result<Guid>>, LogHabitEntryCommandHandler>();
builder.Services.AddScoped<IQueryHandler<GetHabitEntriesQuery, Result<IReadOnlyList<HabitEntryDto>>>, GetHabitEntriesQueryHandler>();

var app = builder.Build();

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

app.MapControllers();
app.Run();

public partial class Program;
