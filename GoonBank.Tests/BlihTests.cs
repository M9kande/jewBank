using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GoonBank.DTOs;
using GoonBank.Enums;
using GoonBank.Models;
using Xunit;

namespace GoonBank.Tests;

public class BlihTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BlihTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string AccountId, string Token)> RegisterAndLoginAsync()
    {
        var email = $"blih_{Guid.NewGuid():N}@bank.com";
        var password = "BlihPassword123!";

        var reg = await _client.PostAsJsonAsync("/api/Account", new CreateAccountRequest
        {
            FirstName = "BlihUser",
            LastName = "Test",
            Email = email,
            PasswordHash = password,
            DateOfBirth = new DateOnly(1993, 7, 7),
            Currency = "PLN",
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
    public async Task Full_Blih_Payment_Flow_Succeeds()
    {
        var (payerId, payerToken) = await RegisterAndLoginAsync();
        var (merchantId, merchantToken) = await RegisterAndLoginAsync();

        // 1. Give payer some funds (1000 PLN)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payerToken);
        var depResp = await _client.PostAsJsonAsync("/api/Transaction", new CreateTransactionRequest
        {
            SourceAccountId = payerId,
            Amount = 1000.00m,
            TransactionType = TransactionType.Deposit,
            Description = "Deposit for BLIH tests"
        });
        var depTx = await depResp.Content.ReadFromJsonAsync<MoneyTransaction>();
        await _client.PutAsync($"/api/Transaction/{depTx!.TransactionId}/execute", null);

        // 2. Register BLIH Profile for payer
        var phoneHash = Guid.NewGuid().ToString("N");
        var profResp = await _client.PostAsJsonAsync("/api/BlihProfile", new CreateBlihProfileRequest
        {
            AccountId = payerId,
            PhoneHash = phoneHash
        });
        Assert.Equal(HttpStatusCode.OK, profResp.StatusCode);

        // 3. Generate BLIH code
        var codeResp = await _client.PostAsJsonAsync("/api/Blih/code", new GenerateBlihCodeRequest
        {
            AccountId = payerId
        });
        Assert.Equal(HttpStatusCode.OK, codeResp.StatusCode);
        var codeData = await codeResp.Content.ReadFromJsonAsync<BlihCodeResponse>();
        Assert.NotNull(codeData);
        Assert.Equal(6, codeData.Code.Length);

        // 4. Merchant initiates payment (Merchant can be unauthenticated or merchant credentials)
        _client.DefaultRequestHeaders.Authorization = null;
        var initResp = await _client.PostAsJsonAsync("/api/Blih/payment/initiate", new InitiateBlihPaymentRequest
        {
            BlihCode = codeData.Code,
            MerchantId = merchantId,
            Amount = 150.00m
        });
        Assert.Equal(HttpStatusCode.OK, initResp.StatusCode);
        var initJson = await initResp.Content.ReadFromJsonAsync<JsonElement>();
        var paymentId = initJson.GetProperty("paymentId").GetString()!;

        // 5. Customer checks pending payment
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payerToken);
        var pendingResp = await _client.GetAsync($"/api/Blih/payment/pending/{payerId}");
        Assert.Equal(HttpStatusCode.OK, pendingResp.StatusCode);
        var pendingJson = await pendingResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(paymentId, pendingJson.GetProperty("blihPaymentId").GetString());

        // 6. Customer confirms payment
        var confirmResp = await _client.PostAsync($"/api/Blih/payment/{paymentId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        // 7. Verify balances
        var payerAccResp = await _client.GetAsync($"/api/Account/{payerId}");
        var payerAcc = await payerAccResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(850.00m, payerAcc!.Balance); // 1000 - 150

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantToken);
        var merchantAccResp = await _client.GetAsync($"/api/Account/{merchantId}");
        var merchantAcc = await merchantAccResp.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(150.00m, merchantAcc!.Balance); // 0 + 150

        // 8. Verify MoneyTransaction exists in transaction history
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payerToken);
        var txsResp = await _client.GetAsync($"/api/Account/{payerId}/transactions");
        var txs = await txsResp.Content.ReadFromJsonAsync<List<MoneyTransaction>>();
        Assert.NotNull(txs);
        var blihTx = txs.FirstOrDefault(t => t.Type == TransactionType.Blih);
        Assert.NotNull(blihTx);
        Assert.Equal(150.00m, blihTx.Amount);
        Assert.Equal(TransactionStatus.Completed, blihTx.Status);
    }

    [Fact]
    public async Task Duplicate_Phone_Registration_IsBlocked()
    {
        var (user1Id, user1Token) = await RegisterAndLoginAsync();
        var (user2Id, user2Token) = await RegisterAndLoginAsync();

        var sharedPhone = Guid.NewGuid().ToString("N");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        var resp1 = await _client.PostAsJsonAsync("/api/BlihProfile", new CreateBlihProfileRequest
        {
            AccountId = user1Id,
            PhoneHash = sharedPhone
        });
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);
        var resp2 = await _client.PostAsJsonAsync("/api/BlihProfile", new CreateBlihProfileRequest
        {
            AccountId = user2Id,
            PhoneHash = sharedPhone
        });
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }
}
