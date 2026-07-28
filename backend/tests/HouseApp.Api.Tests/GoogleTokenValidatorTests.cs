using HouseApp.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Exercises the real validator (not the fake) for the one behaviour that doesn't need to call
/// Google: whether it considers itself configured. A missing client ID app setting is the most
/// likely deployment mistake, and it must be distinguishable from a bad token.
/// </summary>
public class GoogleTokenValidatorTests
{
    private static GoogleTokenValidator CreateValidator(string? clientId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Google:ClientId"] = clientId })
            .Build();

        return new GoogleTokenValidator(configuration, NullLogger<GoogleTokenValidator>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfigured_IsFalse_WhenClientIdIsMissingOrBlank(string? clientId)
    {
        Assert.False(CreateValidator(clientId).IsConfigured);
    }

    [Fact]
    public void IsConfigured_IsTrue_WhenClientIdIsSet()
    {
        Assert.True(CreateValidator("something.apps.googleusercontent.com").IsConfigured);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNull_WhenNotConfigured()
    {
        // Must not attempt to reach Google when there's no audience to validate against.
        Assert.Null(await CreateValidator(null).ValidateAsync("any-token"));
    }
}
