using System.Text;
using dotkit.Models;
using Serilog;

namespace dotkit.Services;

public class ProgramEditor
{
    private readonly ProjectInfo _project;
    private readonly ILogger _logger;

    public ProgramEditor(ProjectInfo project)
    {
        _project = project;
        _logger = Log.ForContext<ProgramEditor>();
    }

    public async Task<string> UpdateProgramAsync()
    {
        var programPath = _project.ProgramPath;

        _logger.Information("Updating {Path}", programPath);

        string content = await File.ReadAllTextAsync(programPath);

        if (ContainsJwtConfiguration(content))
        {
            _logger.Information("JWT already configured in Program.cs, skipping");
            return content;
        }

        string updatedContent = InjectJwtConfiguration(content, _project.Name);
        await File.WriteAllTextAsync(programPath, updatedContent);
        _logger.Information("Program.cs updated with JWT Authentication");
        return updatedContent;
    }

    public static bool ContainsJwtConfiguration(string content)
    {
        return content.Contains("AddAuthentication") &&
               content.Contains("AddJwtBearer");
    }

    public static string InjectJwtConfiguration(string content, string namespaceName)
    {
        var lines = content.Split('\n');
        var newLines = new List<string>();
        bool hasServiceInjection = false;
        int buildIndex = -1;
        bool usingAdded = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (!usingAdded)
            {
                if (trimmed.StartsWith("using "))
                {
                    bool nextIsNotUsing = (i + 1 >= lines.Length) || !lines[i + 1].Trim().StartsWith("using ");
                    if (nextIsNotUsing)
                    {
                        if (!content.Contains($"{namespaceName}.Configuration"))
                            newLines.Add($"using {namespaceName}.Configuration;");
                        if (!content.Contains($"{namespaceName}.Services"))
                            newLines.Add($"using {namespaceName}.Services;");
                        usingAdded = true;
                    }
                }
                else if (i == 0 && !trimmed.StartsWith("using "))
                {
                    newLines.Add($"using {namespaceName}.Configuration;");
                    newLines.Add($"using {namespaceName}.Services;");
                    newLines.Add("");
                    usingAdded = true;
                }
            }

            newLines.Add(line);
            if (trimmed.Contains("var app = builder.Build") ||
                trimmed.Contains("var app=builder.Build"))
            {
                buildIndex = newLines.Count - 1;
            }

            if (!hasServiceInjection &&
                trimmed.Contains("builder.Services.Add"))
            {
                bool isLastAdd = true;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var nextTrim = lines[j].Trim();
                    if (nextTrim.StartsWith("builder.Services.Add"))
                    {
                        isLastAdd = false;
                        break;
                    }
                    if (nextTrim.Contains("Build"))
                        break;
                }

                if (isLastAdd)
                {
                    var indent = GetIndent(line);
                    newLines.Add("");
                    newLines.Add($"{indent}// JWT Authentication");
                    newLines.Add($"{indent}builder.Services.AddAuthentication(\"Bearer\")");
                    newLines.Add($"{indent}    .AddJwtBearer(\"Bearer\", options =>");
                    newLines.Add($"{indent}    {{");
                    newLines.Add($"{indent}        var jwtSettings = builder.Configuration.GetSection(\"Jwt\").Get<JwtSettings>();");
                    newLines.Add($"{indent}        if (jwtSettings is null)");
                    newLines.Add($"{indent}            throw new InvalidOperationException(\"JWT settings not configured.\");");
                    newLines.Add($"{indent}");
                    newLines.Add($"{indent}        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters");
                    newLines.Add($"{indent}        {{");
                    newLines.Add($"{indent}            ValidateIssuer = true,");
                    newLines.Add($"{indent}            ValidateAudience = true,");
                    newLines.Add($"{indent}            ValidateLifetime = true,");
                    newLines.Add($"{indent}            ValidateIssuerSigningKey = true,");
                    newLines.Add($"{indent}            ValidIssuer = jwtSettings.Issuer,");
                    newLines.Add($"{indent}            ValidAudience = jwtSettings.Audience,");
                    newLines.Add($"{indent}            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(");
                    newLines.Add($"{indent}                System.Text.Encoding.UTF8.GetBytes(jwtSettings.SecretKey))");
                    newLines.Add($"{indent}        }};");
                    newLines.Add($"{indent}    }});");
                    newLines.Add($"{indent}builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(\"Jwt\"));");
                    newLines.Add($"{indent}builder.Services.AddScoped<JwtService>();");
                    hasServiceInjection = true;
                }
            }
        }

        if (buildIndex >= 0)
        {
            var buildLine = newLines[buildIndex];
            var indent = GetIndent(buildLine);

            newLines.Insert(buildIndex + 1, "");
            newLines.Insert(buildIndex + 2, $"{indent}app.UseAuthentication();");
            newLines.Insert(buildIndex + 3, $"{indent}app.UseAuthorization();");
        }

        return string.Join("\n", newLines);
    }

    private static string GetIndent(string line)
    {
        int i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
            i++;
        return i < line.Length ? line[..i] : "";
    }
}
