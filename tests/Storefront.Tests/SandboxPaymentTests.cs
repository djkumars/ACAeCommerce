using Xunit;

public sealed class SandboxPaymentTests
{
    [Fact]
    public void Authorize_UsesDeterministicSandboxDecision()
    {
        var orderId = Guid.Parse("cd53555b-6e22-4fac-a2f0-5ad7177ccf11");
        Assert.Equal("approved", SandboxPayment.Authorize(new(orderId, 20.00m, "USD")).Status);
        Assert.Equal("declined", SandboxPayment.Authorize(new(orderId, 19.99m, "USD")).Status);
    }

    [Theory]
    [InlineData(0, 20, "USD")]
    [InlineData(1, 0, "USD")]
    [InlineData(1, 20, "EUR")]
    public void IsValid_RejectsInvalidRequest(int nonEmptyOrderId, decimal amount, string currency)
    {
        var id = nonEmptyOrderId == 0 ? Guid.Empty : Guid.NewGuid();
        Assert.False(SandboxPayment.IsValid(new(id, amount, currency)));
    }
}
