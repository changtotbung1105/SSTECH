using System.ComponentModel.DataAnnotations;
using PartnerIntegration.Api.Transactions;
using Xunit;

namespace PartnerIntegration.Tests;

public class ValidationTests
{
    public static TransactionRequest Valid => new()
    {
        PartnerId = "P-1001", TransactionReference = "TXN-99823", Amount = 250m,
        Currency = "USD", Timestamp = DateTimeOffset.Parse("2024-05-10T14:30:00Z")
    };
    private static bool Validate(TransactionRequest request) => Validator.TryValidateObject(request,
        new ValidationContext(request), new List<ValidationResult>(), validateAllProperties: true);

    [Fact] public void Valid_payload_passes() => Assert.True(Validate(Valid));

    public static IEnumerable<object[]> InvalidPayloads()
    {
        yield return new object[] { Valid with { PartnerId = null } };
        yield return new object[] { Valid with { PartnerId = " " } };
        yield return new object[] { Valid with { PartnerId = new string('P', 101) } };
        yield return new object[] { Valid with { TransactionReference = null } };
        yield return new object[] { Valid with { TransactionReference = "" } };
        yield return new object[] { Valid with { TransactionReference = new string('T', 101) } };
        yield return new object[] { Valid with { Amount = null } };
        yield return new object[] { Valid with { Amount = 0 } };
        yield return new object[] { Valid with { Amount = -1 } };
        yield return new object[] { Valid with { Currency = null } };
        yield return new object[] { Valid with { Currency = "" } };
        yield return new object[] { Valid with { Currency = "usd" } };
        yield return new object[] { Valid with { Currency = "XYZ" } };
        yield return new object[] { Valid with { Timestamp = null } };
        yield return new object[] { Valid with { Timestamp = default(DateTimeOffset) } };
    }
    [Theory, MemberData(nameof(InvalidPayloads))]
    public void Invalid_fields_fail(TransactionRequest request) => Assert.False(Validate(request));

    [Theory]
    [InlineData("USD")][InlineData("EUR")][InlineData("GBP")][InlineData("VND")]
    [InlineData("JPY")][InlineData("SGD")][InlineData("AUD")][InlineData("CAD")]
    [InlineData("CHF")][InlineData("CNY")][InlineData("THB")]
    public void Supported_currencies_pass(string currency) => Assert.True(Validate(Valid with { Currency = currency }));

    [Fact] public void Small_positive_amount_passes() => Assert.True(Validate(Valid with { Amount = 0.0001m }));
}
