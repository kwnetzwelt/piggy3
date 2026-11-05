using Piggy3.Shared;

namespace Piggy3.Api.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("53,80", 53.80)]
    [InlineData("-53,80", -53.80)]
    [InlineData("53.80", 53.80)]
    [InlineData(" 1 234,56 € ", 1234.56)]
    [InlineData("0", 0)]
    [InlineData("53,805", 53.81)]
    [InlineData("-0,005", -0.01)]
    public void TryParse_ReturnsRoundedAmount(string input, decimal expected)
    {
        Assert.True(Money.TryParse(input, out var amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("1.234,56")]
    [InlineData("12.")]
    public void TryParse_RejectsInvalidInput(string input)
    {
        Assert.False(Money.TryParse(input, out _));
    }

    [Fact]
    public void Format_UsesGermanNotationAndEuro()
    {
        Assert.Equal("1.234,56 €", Money.Format(1234.56m));
        Assert.Equal("-53,80 €", Money.Format(-53.80m));
        Assert.Equal("0,00 €", Money.Format(0m));
    }

    [Theory]
    [InlineData("53,80", 5380)]
    [InlineData("-53,80", -5380)]
    [InlineData("53,805", 5381)]
    public void Cents_RoundsToBankCents(string input, long expected)
    {
        Assert.True(Money.TryParse(input, out var amount));
        Assert.Equal(expected, Money.Cents(amount));
    }
}
