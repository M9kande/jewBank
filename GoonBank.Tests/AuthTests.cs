using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GoonBank.DTOs;
using GoonBank.Enums;
using Xunit;

namespace GoonBank.Tests;

public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_And_Login_Successful_ReturnsJwtToken()
    {
        var email = $"user_{Guid.NewGuid():N}@bank.com";
        var password = "SecurePassword123!";

        // 1. Register Account
        var createRequest = new CreateAccountRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = email,
            PasswordHash = password,
            DateOfBirth = new DateOnly(1992, 4, 10),
            Currency = "PLN",
            AccountType = AccountType.Checking
        };

        var regResp = await _client.PostAsJsonAsync("/api/Account", createRequest);
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);

        // 2. Login
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var authData = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authData);
        Assert.False(string.IsNullOrWhiteSpace(authData.Token));
        Assert.Equal(email.ToLowerInvariant(), authData.Email.ToLowerInvariant());

        // 3. Access protected /api/auth/me with token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);
        var meResp = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        var meData = await meResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(meData);
        Assert.Equal(authData.AccountId, meData.AccountId);
    }

    [Fact]
    public async Task Login_With_WrongPassword_ReturnsUnauthorized()
    {
        var email = $"user_{Guid.NewGuid():N}@bank.com";

        await _client.PostAsJsonAsync("/api/Account", new CreateAccountRequest
        {
            FirstName = "Alice",
            LastName = "Wonder",
            Email = email,
            PasswordHash = "CorrectPassword123!",
            DateOfBirth = new DateOnly(1995, 1, 1),
            Currency = "PLN",
            AccountType = AccountType.Savings
        });

        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "WrongPassword999!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }

    [Fact]
    public async Task Login_With_NonExistentEmail_ReturnsUnauthorized()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "nonexistent@bank.com",
            Password = "AnyPassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }

    [Fact]
    public async Task Access_Me_WithoutToken_ReturnsUnauthorized()
    {
        using var unauthClient = new HttpClient();
        var resp = await _client.GetAsync("/api/auth/me");
        // Clear auth header if any
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthResp = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);
    }
}
