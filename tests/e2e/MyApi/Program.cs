using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/token", (JwtService jwtService) =>
    Results.Ok(new { token = jwtService.GenerateAccessToken(1, "e2e", "e2e@example.com", "user") }));

app.MapGet("/protected", [Authorize] () => Results.Ok(new { message = "protected" }));

app.Run();
