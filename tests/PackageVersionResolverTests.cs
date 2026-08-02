using System.Net;
using System.Text;
using dotkit.Services;
using Xunit;

namespace dotkit.Tests;

public class PackageVersionResolverTests
{
    private static PackageVersionResolver CreateResolver(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(responseJson, statusCode);
        var client = new HttpClient(handler);
        return new PackageVersionResolver(client);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsLatestStable_ForMajor()
    {
        var json = """{"versions":["6.0.0","6.0.28","6.0.36","6.0.5"]}""";
        var resolver = CreateResolver(json);
        var version = await resolver.ResolveAsync("Microsoft.AspNetCore.Authentication.JwtBearer", 6);
        Assert.Equal("6.0.36", version);
    }

    [Fact]
    public async Task ResolveAsync_IgnoresPrereleases()
    {
        var json = """{"versions":["8.0.0","8.0.0-preview.1","8.0.0-rc.1","8.0.29"]}""";
        var resolver = CreateResolver(json);
        var version = await resolver.ResolveAsync("Test.Package", 8);
        Assert.Equal("8.0.29", version);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToKnownVersion_WhenHttpFails()
    {
        var resolver = CreateResolver("", HttpStatusCode.InternalServerError);
        var version = await resolver.ResolveAsync("Test.Package", 8);
        Assert.Equal("8.0.29", version);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsEmpty_WhenMajorHasNoKnownVersion()
    {
        var resolver = CreateResolver("", HttpStatusCode.InternalServerError);
        var version = await resolver.ResolveAsync("Test.Package", 11);
        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsEmpty_WhenMajorNotPositive()
    {
        var resolver = CreateResolver("""{"versions":["10.0.0"]}""");
        var version = await resolver.ResolveAsync("Test.Package", 0);
        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public async Task ResolveAsync_UsesLowercasePackageId_ForUrl()
    {
        var json = """{"versions":["7.0.0","7.0.20"]}""";
        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var resolver = new PackageVersionResolver(client);
        var version = await resolver.ResolveAsync("Microsoft.AspNetCore.Authentication.JwtBearer", 7);
        Assert.Equal("7.0.20", version);
        Assert.EndsWith("microsoft.aspnetcore.authentication.jwtbearer/index.json", handler.LastRequestUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../evil")]
    [InlineData("evil/path")]
    [InlineData("pack;age")]
    [InlineData("has space")]
    [InlineData("conn\\ection")]
    [InlineData("-starts-with-symbol")]
    [InlineData("ends-with.dot.")]
    public async Task ResolveAsync_RejectsInvalidPackageId_WithoutNetworkCall(string packageId)
    {
        var handler = new FakeHttpMessageHandler("""{"versions":["8.0.0"]}""");
        var resolver = new PackageVersionResolver(new HttpClient(handler));

        var version = await resolver.ResolveAsync(packageId, 8);

        Assert.Equal(string.Empty, version);
        Assert.Null(handler.LastRequestUrl);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public string? LastRequestUrl { get; private set; }

        public FakeHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUrl = request.RequestUri?.ToString();
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
