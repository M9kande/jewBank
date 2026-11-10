using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GoonBank.DTOs;
using GoonBank.Enums;
using Xunit;

namespace GoonBank.Tests;

public class AccountTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string AccountId, string Token)> RegisterAndLoginAsync(string? email = null)
    {
        var mail = email ?? $"acc_{Guid.NewGuid():N}@bank.com";
        var password = "TestPassword123!";

        var reg = await _client.PostAsJsonAsync("/api/Account", new CreateAccountRequest
        {
            FirstName = "Test",
            LastName = "User",
            Email = mail,
            PasswordHash = password,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Currency = "PLN",
            AccountType = AccountType.Checking
        });
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = mail,
            Password = password
        });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.AccountId, auth.Token);
    }

    [Fact]
    public async Task GetAccount_OnlyOwnerCanAccess()
    {
        var (user1Id, user1Token) = await RegisterAndLoginAsync();
        var (user2Id, user2Token) = await RegisterAndLoginAsync();

        // 1. Unauthenticated gets 401
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthResp = await _client.GetAsync($"/api/Account/{user1Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. User 2 trying to access User 1's account gets 403 Forbidden
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        var forbidResp = await _client.GetAsync($"/api/Account/{user1Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidResp.StatusCode);

        // 3. User 1 accessing User 1's account gets 200 OK
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        var okResp = await _client.GetAsync($"/api/Account/{user1Id}");
        Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);
        var account = await okResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(account);
        Assert.Equal(user1Id, account.AccountId);
    }

    [Fact]
    public async Task CreateAccount_DuplicateEmail_ReturnsConflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@bank.com";
        var req = new CreateAccountRequest
        {
            FirstName = "A",
            LastName = "B",
            Email = email,
            PasswordHash = "Pass123!",
            DateOfBirth = new DateOnly(1995, 2, 2),
            Currency = "PLN",
            AccountType = AccountType.Checking
        };

        var resp1 = await _client.PostAsJsonAsync("/api/Account", req);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        var resp2 = await _client.PostAsJsonAsync("/api/Account", req);
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_FutureDateOfBirth_ReturnsBadRequest()
    {
        var req = new CreateAccountRequest
        {
            FirstName = "A",
            LastName = "B",
            Email = $"future_{Guid.NewGuid():N}@bank.com",
            PasswordHash = "Pass123!",
            DateOfBirth = new DateOnly(2099, 1, 1),
            Currency = "PLN",
            AccountType = AccountType.Checking
        };

        var resp = await _client.PostAsJsonAsync("/api/Account", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_OwnerCanFreezeAndUnfreeze()
    {
        var (accountId, token) = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Freeze
        var freezeResp = await _client.PatchAsJsonAsync($"/api/Account/{accountId}/status", AccountStatus.Frozen);
        Assert.Equal(HttpStatusCode.OK, freezeResp.StatusCode);

        // Verify status
        var accResp = await _client.GetAsync($"/api/Account/{accountId}");
        var acc = await accResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(AccountStatus.Frozen, acc!.Status);

        // Unfreeze
        var unfreezeResp = await _client.PatchAsJsonAsync($"/api/Account/{accountId}/status", AccountStatus.Active);
        Assert.Equal(HttpStatusCode.OK, unfreezeResp.StatusCode);
    }
}
