# dotkit

A .NET CLI tool that installs and configures JWT Authentication in ASP.NET Core Web API projects with a single command.

## Features

- Installs `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package
- Adds `Jwt` section to `appsettings.json` with auto-generated secret key
- Creates `JwtSettings` and `JwtService` ready-to-use classes
- Injects JWT authentication, DI registration, and middleware into `Program.cs`
- Configures User Secrets for `SecretKey`, `Issuer`, and `Audience` (or writes them to `appsettings.json` via `--no-user-secrets`)
- Logs progress to console and rolling files via Serilog

## Installation

```bash
dotnet tool install --global dotkit
```

## Usage

```bash
dotkit jwt install [options]
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--project` | Path to the Web API project directory | Current directory |
| `--secret-key` | JWT secret key (32+ chars) | Auto-generated (64 chars base64) |
| `--issuer` | Token issuer | Project name |
| `--audience` | Token audience | Project name |
| `--no-user-secrets` | Write values to `appsettings.json` instead of User Secrets | false |
| `--verbose` | Enable debug-level logging | false |

### Examples

```bash
# Default: stores values in User Secrets
dotkit jwt install

# Custom values
dotkit jwt install --project ./MyApi --issuer "MyApp" --audience "MyApp"

# Write values directly to appsettings.json (skip User Secrets)
dotkit jwt install --no-user-secrets --secret-key "your-32-char-min-key-here..."
```

## What it does

1. Detects the ASP.NET Core Web API project (must target `Microsoft.NET.Sdk.Web`)
2. Installs `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package
3. Merges `Jwt` section into `appsettings.json`
4. Creates `Configuration/JwtSettings.cs` and `Services/JwtService.cs`
5. Injects into `Program.cs`:
   - Required `using` directives
   - `builder.Services.AddAuthentication().AddJwtBearer()` with `TokenValidationParameters`
   - `builder.Services.Configure<JwtSettings>(...)`
   - `builder.Services.AddScoped<JwtService>()`
   - `app.UseAuthentication()` and `app.UseAuthorization()`
6. Configures `SecretKey`, `Issuer`, and `Audience` via User Secrets (or `appsettings.json`)

After running, inject `JwtService` in any controller and call `GenerateAccessToken()` to issue JWTs.

### Secret storage modes

| Mode | `appsettings.json` | User Secrets |
|---|---|---|
| **User Secrets** (default) | Placeholder (`CHANGE_ME...`) | Real key, issuer, audience |
| **`--no-user-secrets`** | Real key, issuer, audience | Not configured |

## Lifecycle

```
dotkit jwt install [options]
             │
             ▼
    ┌─────────────────────┐
    │  Inicializar Serilog │  → %TEMP%\dotkit\logs\dotkit-YYYYMMDD.log
    └─────────┬───────────┘
              ▼
    ┌─────────────────────┐
    │  ProjectDetector    │  ← Reads .csproj from directory
    │  .Detect()          │
    └─────────┬───────────┘
              ▼
    ┌─────────────────────┐
    │  JwtInstaller       │  ← dotnet add package
    │  .InstallAsync()    │     Microsoft.AspNetCore.Authentication.JwtBearer
    └─────────┬───────────┘
              ▼
    ┌─────────────────────┐
    │  JsonEditor         │  ← Merges Jwt section from template
    │  .UpdateAppSettings │     into existing appsettings.json
    │  (key?,iss?,aud?)  │     (real values if --no-user-secrets,
    └─────────┬───────────┘      placeholder if User Secrets)
              ▼
    ┌─────────────────────┐
    │  TemplateInstaller  │  ← Creates:
    │  .InstallTemplates  │     Configuration/JwtSettings.cs
    │                     │     Services/JwtService.cs
    └─────────┬───────────┘
              ▼
    ┌─────────────────────┐
    │  ProgramEditor      │  ← Injects into Program.cs:
    │  .UpdateProgramAsync│     - usings for .Configuration and .Services
    │                     │     - AddAuthentication + AddJwtBearer
    │                     │     - Configure<JwtSettings>
    │                     │     - AddScoped<JwtService>
    │                     │     - app.UseAuthentication/Authorization
    └─────────┬───────────┘
              ▼
    ┌─────────────────────┐
    │  UserSecretsManager  │  ← (only if NOT --no-user-secrets)
    │  .ConfigureAsync    │     dotnet user-secrets init
    │                     │     set Jwt:SecretKey
    │                     │     set Jwt:Issuer
    │                     │     set Jwt:Audience
    └─────────┬───────────┘
              ▼
    ✓ Installation completed successfully
```

## Possible errors

### ProjectDetector

| Error | Cause |
|---|---|
| `Directory '{path}' not found.` | `--project` points to a non-existent directory |
| `No .csproj file was found.` | Directory contains no `.csproj` file |
| `Program.cs was not found.` | Project has no `Program.cs` |
| `The selected project is not an ASP.NET Core Web API.` | `.csproj` doesn't use `Microsoft.NET.Sdk.Web` |

### JsonEditor

| Error | Cause |
|---|---|
| `Template not found: {path}` | `appsettings.json` template missing from `Templates/` |
| `Template JSON is invalid.` | Template is not valid JSON |
| `Template does not contain 'Jwt' section.` | Template has no `Jwt` property |
| `Existing appsettings.json contains invalid JSON.` | Target project's `appsettings.json` has malformed JSON |

### JwtInstaller

| Error | Cause |
|---|---|
| `Failed to install Microsoft.AspNetCore.Authentication.JwtBearer: ...` | `dotnet add package` failed (no connection, incompatible version, etc.) |

### UserSecretsManager

| Error | Cause |
|---|---|
| `Error: dotnet user-secrets init --project ...` | Could not initialize User Secrets (permissions, SDK) |
| `Error: dotnet user-secrets set --project ...` | Could not set a secret |

### Runtime errors (in the generated project, NOT in the tool)

| Error (project runtime) | Cause |
|---|---|
| `JWT settings not configured.` | `Jwt` section in `appsettings.json` was not loaded correctly |
| `IDX10720: key size must be greater than '256' bits` | `SecretKey` < 32 characters (only if someone modified the placeholder) |
| `SecretKey not found.` / `Issuer not found.` / `Audience not found.` | `JwtService` instantiated without configured values |

### Exit code

- `0` → success
- `1` → any error (message and exception are logged)

## Requirements

- .NET 10 SDK or later
- An ASP.NET Core Web API project targeting `net10.0`
