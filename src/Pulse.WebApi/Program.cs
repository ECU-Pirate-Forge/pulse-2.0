using Pulse.WebApi;
using LiteDB;
using Pulse.WebApi.Middleware;
using Pulse.Common.Models;
using Pulse.Common.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("pulse-db") ?? "Filename=pulse.db;Connection=shared";

builder.Services.AddPulseWebApiCoreServices(connectionString);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/questions", (QuestionRepository repo) => repo.GetAll());
app.MapPost("/questions", (QuestionRepository repo, Question q) => repo.Insert(q));

app.MapDefaultEndpoints();

app.Run();
