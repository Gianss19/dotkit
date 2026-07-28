using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

namespace dotkit.Services;

public class JsonEditor
{
    private readonly Models.ProjectInfo _project;
    private readonly ILogger _logger;

    public string TemplatesPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Templates");

    public JsonEditor(Models.ProjectInfo project)
    {
        _project = project;
        _logger = Log.ForContext<JsonEditor>();
    }

    public async Task UpdateAppSettingsAsync(string? secretKey = null, string? issuer = null, string? audience = null)
    {
        var appSettingsPath = _project.AppSettingsPath;
        var templatePath = Path.Combine(TemplatesPath, "appsettings.json");

        _logger.Information("Actualizando {Path}", appSettingsPath);

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template no encontrado: {templatePath}");

        string templateContent = await File.ReadAllTextAsync(templatePath);
        JsonObject templateNode = JsonNode.Parse(templateContent)?.AsObject()
            ?? throw new InvalidDataException("Template JSON inválido.");

        if (!templateNode.TryGetPropertyValue("Jwt", out JsonNode? jwtNode))
            throw new InvalidDataException("El template no contiene sección 'Jwt'.");

        JsonObject appSettingsNode;

        if (File.Exists(appSettingsPath))
        {
            string existingContent = await File.ReadAllTextAsync(appSettingsPath);
            if (string.IsNullOrWhiteSpace(existingContent))
            {
                appSettingsNode = new JsonObject();
            }
            else
            {
                try
                {
                    appSettingsNode = JsonNode.Parse(existingContent)?.AsObject() ?? new JsonObject();
                }
                catch (JsonException ex)
                {
                    _logger.Error(ex, "appsettings.json contiene JSON inválido");
                    throw new InvalidDataException("El appsettings.json existente contiene JSON inválido.", ex);
                }
            }
        }
        else
        {
            appSettingsNode = new JsonObject();
        }

        var jwtConfig = jwtNode!.DeepClone();
        if (jwtConfig is JsonObject jwtObj)
        {
            if (secretKey is not null)
                jwtObj["SecretKey"] = secretKey;
            if (issuer is not null)
                jwtObj["Issuer"] = issuer;
            if (audience is not null)
                jwtObj["Audience"] = audience;
        }

        appSettingsNode["Jwt"] = jwtConfig;

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(appSettingsPath, appSettingsNode.ToJsonString(options));

        _logger.Information("✓ appsettings.json actualizado");
    }
}
