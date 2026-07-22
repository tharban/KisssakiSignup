using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KissakiSignup.Tests;

public class AdminExportTests
{
    [Theory]
    [InlineData("/admin/export/clubs.csv")]
    [InlineData("/admin/export/participants.csv")]
    [InlineData("/admin/export/teams.csv")]
    public async Task GetExport_AsAnonymousUser_RedirectsToLogin(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/admin/login");
    }
}
