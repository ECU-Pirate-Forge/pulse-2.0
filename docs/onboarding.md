# Pulse Onboarding Guide

This guide helps new contributors get the Pulse solution running locally and explains the current dependency injection setup across services.

## 1. What This Solution Contains

Pulse is organized as a multi-project .NET solution with Aspire orchestration:

- Pulse.AppHost: local orchestrator for running solution services together.
- Pulse.WebApi: minimal API backend for question data.
- Pulse.WebApp: student-facing Blazor Server app.
- Pulse.Admin: instructor/admin-facing Blazor Server app.
- Pulse.MigrationManager: background worker project.
- Pulse.Shared: shared models and repositories.
- Pulse.Tests: xUnit test project.

Solution file: Pulse.slnx

## 2. Prerequisites

- .NET SDK 10.0 (the projects target net10.0).
- Git.
- A local development certificate trusted for HTTPS endpoints if you plan to run HTTPS launch profiles.

Optional but recommended:

- Visual Studio 2022 or VS Code with C# Dev Kit.

## 3. First-Time Setup

From the repository root:

1. Restore packages
	 dotnet restore Pulse.slnx
2. Build the solution
	 dotnet build Pulse.slnx
3. Run tests
	 dotnet test src/Pulse.Tests/Pulse.Tests.csproj

## 4. Running the Solution

Recommended path (full local experience): run AppHost

- dotnet run --project src/Pulse.AppHost/Pulse.AppHost.csproj

This launches the distributed app and Aspire dashboard endpoints from the AppHost launch profile.

Run individual projects if needed:

- Web API: dotnet run --project src/Pulse.WebApi/Pulse.WebApi.csproj
- Student app: dotnet run --project src/Pulse.WebApp/Pulse.WebApp.csproj
- Admin app: dotnet run --project src/Pulse.Admin/Pulse.Admin.csproj

Current development URLs from launch settings:

- Web API: http://localhost:5062 and https://localhost:7081
- Web App: http://pulse-webapp.dev.localhost:5243 and https://pulse-webapp.dev.localhost:7004
- Admin App: http://pulse-admin.dev.localhost:5049 and https://pulse-admin.dev.localhost:7019

## 5. Current Dependency Injection Snapshot

This section reflects current registrations in code.

### Pulse.WebApi

In Program.cs, the API registers core services through AddPulseWebApiCoreServices(connectionString).

The extension currently registers:

- LiteDatabase as Singleton
	- Registration: AddSingleton<LiteDatabase>(_ => new LiteDatabase(connectionString))
- IJoinCodeGenerator to JoinCodeGenerator as Singleton
	- Registration: AddSingleton<IJoinCodeGenerator, JoinCodeGenerator>()
- ISessionRepository to SessionRepository as Singleton
	- Registration: AddSingleton<ISessionRepository, SessionRepository>()
- QuestionRepository as Singleton
	- Registration: AddSingleton<QuestionRepository>()
- QuestionService as Singleton
	- Registration: AddSingleton<QuestionService>()
- IQuestionBankRepository to QuestionBankRepository as Singleton
	- Registration: AddSingleton<IQuestionBankRepository, QuestionBankRepository>()
- IResponseRepository to ResponseRepository as Singleton
	- Registration: AddSingleton<IResponseRepository, ResponseRepository>()
- DeviceIdValidationService as Singleton
	- Registration: AddSingleton<DeviceIdValidationService>()

Additional framework registrations in WebApi:

- AddOpenApi()
- Default service provider validation on build is enabled in development.

### Pulse.WebApp

Current registrations:

- AddRazorComponents().AddInteractiveServerComponents()
- AddMudServices()

### Pulse.Admin

Current registrations:

- AddRazorComponents().AddInteractiveServerComponents()
- AddMudServices()

### Pulse.MigrationManager

Current registrations:

- AddHostedService<Worker>()

## 6. Testing Notes

Pulse.Tests uses xUnit v3.

Current smoke coverage includes a DI registration test that verifies ISessionRepository resolves from the container:

- SessionRepositorySmokeTests.AddPulseWebApiCoreServices_RegistersSessionRepository

Run it with:

- dotnet test src/Pulse.Tests/Pulse.Tests.csproj

## 7. Where to Look Next

- docs/architecture.md for deployment and activity-flow diagrams.
- docs/api.md for endpoint details (expand this as endpoints evolve).
- docs/troubleshooting.md for common setup and runtime issues.
