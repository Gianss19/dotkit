using System.Security.Cryptography;
using DotMake.CommandLine;
using dotkit.Services;
using Serilog;
using Serilog.Events;

namespace dotkit.Commands;

[CliCommand(Description = "dotkit - CLI tool to install JWT Authentication in ASP.NET Core Web API projects.")]
public class RootCommand
{
    [CliCommand(Description = "Install and configure JWT Authentication in an ASP.NET Core Web API project.", Name = "install")]
    public class InstallCommand
    {
        [CliOption(Description = "Path to the Web API project directory (default: current directory).", Required = false)]
        public string? Project { get; set; }

        [CliOption(Description = "JWT secret key (auto-generated if not provided).", Required = false)]
        public string? SecretKey { get; set; }

        [CliOption(Description = "JWT token issuer (default: project name).", Required = false)]
        public string? Issuer { get; set; }

        [CliOption(Description = "JWT token audience (default: project name).", Required = false)]
        public string? Audience { get; set; }

        [CliOption(Description = "Skip User Secrets configuration.")]
        public bool NoUserSecrets { get; set; } = false;

        [CliOption(Description = "Show detailed output.")]
        public bool Verbose { get; set; } = false;

        public async Task RunAsync()
        {
            var logLevel = Verbose ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(Path.GetTempPath(), "dotkit", "logs", "dotkit-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .CreateLogger();

            var logger = Log.ForContext<InstallCommand>();
            var projectDir = Project ?? Directory.GetCurrentDirectory();

            logger.Information("╔══════════════════════════════════════════════╗");
            logger.Information("║       dotkit - JWT Installer CLI             ║");
            logger.Information("╚══════════════════════════════════════════════╝");
            logger.Information("Directory: {ProjectDir}", projectDir);

            try
            {
                var detector = new ProjectDetector(projectDir);
                var project = detector.Detect();

                logger.Information("Project: {Name}", project.Name);
                logger.Information("Web API: {IsWebApi}", project.IsWebApi);
                logger.Information("User Secrets: {HasUserSecrets}", project.HasUserSecrets);

                var issuer = Issuer ?? project.Name;
                var audience = Audience ?? project.Name;

                logger.Information("Installing package Microsoft.AspNetCore.Authentication.JwtBearer...");
                var jwtInstaller = new JwtInstaller(project);
                await jwtInstaller.InstallAsync();

                logger.Information("Updating appsettings.json...");
                var jsonEditor = new JsonEditor(project);
                if (NoUserSecrets)
                {
                    var secretKey = SecretKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                    await jsonEditor.UpdateAppSettingsAsync(secretKey, issuer, audience);
                    logger.Warning("The secret key was written to appsettings.json. Do not commit it to source control; use User Secrets or environment variables in production.");
                }
                else
                {
                    await jsonEditor.UpdateAppSettingsAsync();
                }

                logger.Information("Installing JwtSettings and JwtService templates...");
                var templateInstaller = new TemplateInstaller(project);
                await templateInstaller.InstallTemplatesAsync();

                logger.Information("Updating Program.cs...");
                var programEditor = new ProgramEditor(project);
                await programEditor.UpdateProgramAsync();

                if (!NoUserSecrets)
                {
                    var secretKey = SecretKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                    logger.Information("Configuring User Secrets...");
                    var secretsManager = new UserSecretsManager(project);
                    await secretsManager.ConfigureAsync(secretKey, issuer, audience);
                }

                logger.Information("");
                logger.Information("✓ Installation completed successfully!");
                logger.Information("");
                logger.Information("Summary:");
                logger.Information("  - NuGet Package: Microsoft.AspNetCore.Authentication.JwtBearer");
                logger.Information("  - Issuer: {Issuer}", issuer);
                logger.Information("  - Audience: {Audience}", audience);
                logger.Information("  - User Secrets: {UserSecrets}", NoUserSecrets ? "No" : "Yes");
                logger.Information("");
                logger.Information("Next steps:");
                logger.Information("  1. Review appsettings.json and Program.cs");
                logger.Information("  2. Add [Authorize] to your controllers");
                logger.Information("  3. Use JwtService to generate tokens");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error: {Message}", ex.Message);
                Environment.ExitCode = 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
