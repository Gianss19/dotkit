using System.Security.Cryptography;
using DotMake.CommandLine;
using dotkit.Services;
using Serilog;
using Serilog.Events;

namespace dotkit.Commands;

[CliCommand(Description = "dotkit - CLI tool to install JWT Authentication in ASP.NET Core Web API projects.")]
public class RootCommand
{
    [CliCommand(Description = "Instala y configura JWT Authentication en un proyecto ASP.NET Core Web API.", Name = "install")]
    public class InstallCommand
    {
        [CliOption(Description = "Ruta al directorio del proyecto Web API (por defecto: directorio actual).", Required = false)]
        public string? Project { get; set; }

        [CliOption(Description = "Clave secreta JWT (se genera automáticamente si no se proporciona).", Required = false)]
        public string? SecretKey { get; set; }

        [CliOption(Description = "Emisor del token JWT (por defecto: nombre del proyecto).", Required = false)]
        public string? Issuer { get; set; }

        [CliOption(Description = "Audiencia del token JWT (por defecto: nombre del proyecto).", Required = false)]
        public string? Audience { get; set; }

        [CliOption(Description = "No configurar User Secrets.")]
        public bool NoUserSecrets { get; set; } = false;

        [CliOption(Description = "Mostrar información detallada.")]
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
            logger.Information("║       dotkit - JWT Installer CLI            ║");
            logger.Information("╚══════════════════════════════════════════════╝");
            logger.Information("Directorio: {ProjectDir}", projectDir);

            try
            {
                var detector = new ProjectDetector(projectDir);
                var project = detector.Detect();

                logger.Information("Proyecto: {Name}", project.Name);
                logger.Information("Web API: {IsWebApi}", project.IsWebApi);
                logger.Information("User Secrets: {HasUserSecrets}", project.HasUserSecrets);

                var issuer = Issuer ?? project.Name;
                var audience = Audience ?? project.Name;

                logger.Information("Instalando paquete Microsoft.AspNetCore.Authentication.JwtBearer...");
                var jwtInstaller = new JwtInstaller(project);
                await jwtInstaller.InstallAsync();

                logger.Information("Actualizando appsettings.json...");
                var jsonEditor = new JsonEditor(project);
                if (NoUserSecrets)
                {
                    var secretKey = SecretKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                    await jsonEditor.UpdateAppSettingsAsync(secretKey, issuer, audience);
                }
                else
                {
                    await jsonEditor.UpdateAppSettingsAsync();
                }

                logger.Information("Instalando plantillas JwtSettings y JwtService...");
                var templateInstaller = new TemplateInstaller(project);
                await templateInstaller.InstallTemplatesAsync();

                logger.Information("Modificando Program.cs...");
                var programEditor = new ProgramEditor(project);
                await programEditor.UpdateProgramAsync();

                if (!NoUserSecrets)
                {
                    var secretKey = SecretKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                    logger.Information("Configurando User Secrets...");
                    var secretsManager = new UserSecretsManager(project);
                    await secretsManager.ConfigureAsync(secretKey, issuer, audience);
                }

                logger.Information("");
                logger.Information("✓ Instalación completada exitosamente!");
                logger.Information("");
                logger.Information("Resumen:");
                logger.Information("  - Paquete NuGet: Microsoft.AspNetCore.Authentication.JwtBearer");
                logger.Information("  - Issuer: {Issuer}", issuer);
                logger.Information("  - Audience: {Audience}", audience);
                logger.Information("  - User Secrets: {UserSecrets}", NoUserSecrets ? "No" : "Sí");
                logger.Information("");
                logger.Information("Siguientes pasos:");
                logger.Information("  1. Revisa appsettings.json y Program.cs");
                logger.Information("  2. Agrega [Authorize] a tus controladores");
                logger.Information("  3. Usa JwtService para generar tokens");
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
