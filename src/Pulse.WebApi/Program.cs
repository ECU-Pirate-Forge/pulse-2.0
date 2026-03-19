using Pulse.WebApi;
using LiteDB;
using Pulse.Shared.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("pulse-db") ?? "Filename=pulse.db;Connection=shared";

builder.Services.AddPulseWebApiCoreServices(connectionString);
builder.Services.AddControllers(); // needed for SessionsController

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers(); // needed for SessionsController

app.MapGet("/questions", (QuestionRepository repo) => repo.GetAll());
app.MapPost("/questions", (QuestionRepository repo, Question q) => repo.Insert(q));

app.MapDefaultEndpoints();

app.Run();