using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GoonBank.DTOs;
using GoonBank.Enums;
using GoonBank.Models;
using Xunit;

namespace GoonBank.Tests;

public class TransactionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TransactionTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string AccountId, string Token)> RegisterAndLoginAsync(string currency = "PLN")
    {
        var email = $"tx_{Guid.NewGuid():N}@bank.com";
        var password = "TxPassword123!";

        var reg = await _client.PostAsJsonAsync("/api/Account", new CreateAccountRequest
        {
            FirstName = "TxUser",
            LastName = "Test",
            Email = email,
            PasswordHash = password,
            DateOfBirth = new DateOnly(1991, 3, 3),
            Currency = currency,
            AccountType = AccountType.Checking
        });
        reg.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.AccountId, auth.Token);
    }

    [Fact]
    public async Task Deposit_And_Withdrawal_Flow_Succeeds()
    {
        var (accountId, token) = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create Deposit
        var depReq = new CreateTransactionRequest
        {
            SourceAccountId = accountId,
            Amount = 500.00m,
            TransactionType = TransactionType.Deposit,
            Description = "Deposit cash"
        };
        var createResp = await _client.PostAsJsonAsync("/api/Transaction", depReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var tx = await createResp.Content.ReadFromJsonAsync<MoneyTransaction>();

        // 2. Execute Deposit
        var execResp = await _client.PutAsync($"/api/Transaction/{tx!.TransactionId}/execute", null);
        Assert.Equal(HttpStatusCode.OK, execResp.StatusCode);

        // Verify balance = 500
        var accResp = await _client.GetAsync($"/api/Account/{accountId}");
        var acc = await accResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(500.00m, acc!.Balance);

        // 3. Create Withdrawal of 200
        var wReq = new CreateTransactionRequest
        {
            SourceAccountId = accountId,
            Amount = 200.00m,
            TransactionType = TransactionType.Withdrawal,
            Description = "ATM withdrawal"
        };
        var wResp = await _client.PostAsJsonAsync("/api/Transaction", wReq);
        var wTx = await wResp.Content.ReadFromJsonAsync<MoneyTransaction>();

        // Execute Withdrawal
        var wExec = await _client.PutAsync($"/api/Transaction/{wTx!.TransactionId}/execute", null);
        Assert.Equal(HttpStatusCode.OK, wExec.StatusCode);

        // Verify balance = 300
        accResp = await _client.GetAsync($"/api/Account/{accountId}");
        acc = await accResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(300.00m, acc!.Balance);
    }

    [Fact]
    public async Task Withdrawal_Overdraft_IsBlocked()
    {
        var (accountId, token) = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var wReq = new CreateTransactionRequest
        {
            SourceAccountId = accountId,
            Amount = 50000.00m, // Balance is 0
            TransactionType = TransactionType.Withdrawal,
            Description = "Overdraft attempt"
        };
        var wResp = await _client.PostAsJsonAsync("/api/Transaction", wReq);
        var wTx = await wResp.Content.ReadFromJsonAsync<MoneyTransaction>();

        var execResp = await _client.PutAsync($"/api/Transaction/{wTx!.TransactionId}/execute", null);
        Assert.Equal(HttpStatusCode.BadRequest, execResp.StatusCode);
    }

    [Fact]
    public async Task Transfer_BetweenAccounts_Succeeds_Atomically()
    {
        var (user1Id, user1Token) = await RegisterAndLoginAsync();
        var (user2Id, user2Token) = await RegisterAndLoginAsync();

        // Deposit into user 1
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        var depReq = new CreateTransactionRequest
        {
            SourceAccountId = user1Id,
            Amount = 1000.00m,
            TransactionType = TransactionType.Deposit,
            Description = "Initial deposit"
        };
        var depResp = await _client.PostAsJsonAsync("/api/Transaction", depReq);
        var depTx = await depResp.Content.ReadFromJsonAsync<MoneyTransaction>();
        await _client.PutAsync($"/api/Transaction/{depTx!.TransactionId}/execute", null);

        // Transfer 400 from User 1 to User 2
        var xferReq = new CreateTransactionRequest
        {
            SourceAccountId = user1Id,
            TargetAccountId = user2Id,
            Amount = 400.00m,
            TransactionType = TransactionType.Transfer,
            Description = "Transfer to friend"
        };
        var xferResp = await _client.PostAsJsonAsync("/api/Transaction", xferReq);
        Assert.Equal(HttpStatusCode.Created, xferResp.StatusCode);
        var xferTx = await xferResp.Content.ReadFromJsonAsync<MoneyTransaction>();

        var execResp = await _client.PutAsync($"/api/Transaction/{xferTx!.TransactionId}/execute", null);
        Assert.Equal(HttpStatusCode.OK, execResp.StatusCode);

        // Check user 1 balance = 600
        var acc1Resp = await _client.GetAsync($"/api/Account/{user1Id}");
        var acc1 = await acc1Resp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(600.00m, acc1!.Balance);

        // Check user 2 balance = 400
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        var acc2Resp = await _client.GetAsync($"/api/Account/{user2Id}");
        var acc2 = await acc2Resp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(400.00m, acc2!.Balance);
    }

    [Fact]
    public async Task Transfer_CrossCurrency_IsBlocked()
    {
        var (user1Id, user1Token) = await RegisterAndLoginAsync("PLN");
        var (user2Id, _) = await RegisterAndLoginAsync("EUR");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        var xferReq = new CreateTransactionRequest
        {
            SourceAccountId = user1Id,
            TargetAccountId = user2Id,
            Amount = 100.00m,
            TransactionType = TransactionType.Transfer,
            Description = "Cross currency attempt"
        };
        var resp = await _client.PostAsJsonAsync("/api/Transaction", xferReq);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task UserCannotCreateTransactionFromAnotherUsersAccount()
    {
        var (user1Id, _) = await RegisterAndLoginAsync();
        var (_, user2Token) = await RegisterAndLoginAsync();

        // User 2 attempts to initiate a transaction using User 1's source account
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        var maliciousReq = new CreateTransactionRequest
        {
            SourceAccountId = user1Id,
            Amount = 50.00m,
            TransactionType = TransactionType.Withdrawal,
            Description = "Malicious withdrawal"
        };

        var resp = await _client.PostAsJsonAsync("/api/Transaction", maliciousReq);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task StatusTampering_DirectlyPatchingToCompleted_IsBlocked()
    {
        var (accountId, token) = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var req = new CreateTransactionRequest
        {
            SourceAccountId = accountId,
            Amount = 10.00m,
            TransactionType = TransactionType.Deposit,
            Description = "Tamper check"
        };
        var resp = await _client.PostAsJsonAsync("/api/Transaction", req);
        var tx = await resp.Content.ReadFromJsonAsync<MoneyTransaction>();

        // Attempt to PATCH status directly to Completed
        var tamperResp = await _client.PatchAsJsonAsync($"/api/Transaction/{tx!.TransactionId}", TransactionStatus.Completed);
        Assert.Equal(HttpStatusCode.BadRequest, tamperResp.StatusCode);
    }
}
