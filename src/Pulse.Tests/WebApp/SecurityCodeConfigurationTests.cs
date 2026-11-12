using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Pulse.Admin.Components;

namespace Pulse.Tests.WebApp;

public sealed class SecurityCodeConfigurationTests : BunitContext
{
    public SecurityCodeConfigurationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    public sealed class AdminUserSecretsTests
    {
        [Theory]
        [InlineData(null, null, "secret-instructor", "secret-admin")]
        [InlineData("host-instructor", null, "host-instructor", "secret-admin")]
        [InlineData(null, "host-admin", "secret-instructor", "host-admin")]
        [InlineData("host-instructor", "host-admin", "host-instructor", "host-admin")]
        public void UserSecrets_LoadOutsideDevelopmentWithPerCodeEnvironmentPrecedence(
            string? environmentInstructorCode, string? environmentAdminCode,
            string expectedInstructorCode, string expectedAdminCode)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"pulse-admin-secrets-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var secretsFile = Path.Combine(directory, "secrets.json");
            File.WriteAllText(secretsFile, """
                {
                    "Security:InstructorCode": "secret-instructor",
                    "Security:AdminCode": "secret-admin"
                }
                """);

            try
            {
                using var files = new PhysicalFileProvider(directory);
                using var factory = new WebApplicationFactory<App>()
                    .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
                var configuration = Assert.IsType<ConfigurationManager>(
                    factory.Services.GetRequiredService<IConfiguration>());
                var environmentValues = new Dictionary<string, string?>();
                if (environmentInstructorCode is not null)
                    environmentValues["Security:InstructorCode"] = environmentInstructorCode;
                if (environmentAdminCode is not null)
                    environmentValues["Security:AdminCode"] = environmentAdminCode;

                var secretsSources = 0;
                for (var index = 0; index < configuration.Sources.Count; index++)
                {
                    var source = configuration.Sources[index];
                    if (source is JsonConfigurationSource { Path: "secrets.json" })
                    {
                        configuration.Sources[index] = new JsonConfigurationSource
                        {
                            FileProvider = files,
                            Path = "secrets.json",
                            Optional = false
                        };
                        secretsSources++;
                    }
                    else if (source is EnvironmentVariablesConfigurationSource { Prefix: null or "" })
                    {
                        configuration.Sources[index] = new MemoryConfigurationSource
                        {
                            InitialData = environmentValues
                        };
                    }
                }

                Assert.True(secretsSources > 0, "Admin must register user-secrets outside Development.");
                Assert.Equal(expectedInstructorCode, configuration["Security:InstructorCode"]);
                Assert.Equal(expectedAdminCode, configuration["Security:AdminCode"]);
            }
            finally
            {
                File.Delete(secretsFile);
                Directory.Delete(directory);
            }
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "admin")]
    [InlineData("instructor", null)]
    [InlineData("", "admin")]
    [InlineData("instructor", "")]
    [InlineData(" ", "admin")]
    [InlineData("instructor", " ")]
    public void MissingCodes_ShowUserSecretsConfigurationError(string? instructorCode, string? adminCode)
    {
        Configure(instructorCode, adminCode);

        var cut = Render<Routes>();

        var error = cut.Find("[role='alert']").TextContent;
        Assert.Contains("Security:InstructorCode", error);
        Assert.Contains("Security:AdminCode", error);
        Assert.Contains("Pulse.AppHost user-secrets", error);
        Assert.Contains("Pulse.Admin user-secrets", error);
        Assert.Contains("Restart", error);
        Assert.DoesNotContain("Not Found", cut.Markup);
    }

    [Fact]
    public void ConfiguredCodes_RenderRequestedPageWithoutExposingCodes()
    {
        Configure("private-instructor-code", "private-admin-code");

        var cut = Render<Routes>();

        Assert.Contains("Not Found", cut.Markup);
        Assert.DoesNotContain("Security codes need to be configured", cut.Markup);
        Assert.DoesNotContain("private-instructor-code", cut.Markup);
        Assert.DoesNotContain("private-admin-code", cut.Markup);
    }

    private void Configure(string? instructorCode, string? adminCode)
    {
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:InstructorCode"] = instructorCode,
                ["Security:AdminCode"] = adminCode
            })
            .Build());
        Services.GetRequiredService<NavigationManager>().NavigateTo("/not-found");
    }
}
