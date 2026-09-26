using System.Globalization;
using Aion.Components.Querying.Results;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class ResultValueFormatterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static string? Display(object? value, string? type = null) => ResultValueFormatter.Display(value, type, Invariant);

    [Theory]
    [InlineData(259.96999999999997, "259.97")]
    [InlineData(234.95000000000002, "234.95")]
    [InlineData(0.30000000000000004, "0.3")]
    [InlineData(3.14159265358979, "3.141593")]
    [InlineData(-12.5, "-12.5")]
    [InlineData(1.0, "1")]
    [InlineData(0.0, "0")]
    [InlineData(55000.00000000001, "55000")]
    public void Double_ShowsAtMostSixDecimalsWithoutTrailingZeros(double value, string expected)
    {
        Display(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.0000001234, "1.234E-07")]
    [InlineData(-0.0000001234, "-1.234E-07")]
    public void Double_TooSmallForSixDecimals_KeepsItsSignificantDigits(double value, string expected)
    {
        Display(value).ShouldBe(expected);
    }

    [Fact]
    public void Double_TooLargeForFixedDigits_UsesTheShortestExactForm()
    {
        Display(1e20).ShouldBe("1E+20");
    }

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public void Double_NotFinite_ShowsItsName(double value, string expected)
    {
        Display(value).ShouldBe(expected);
    }

    [Fact]
    public void Float_IsRoundedFromItsOwnPrecision()
    {
        Display(0.1f).ShouldBe("0.1");
        Display(2.675f).ShouldBe("2.675");
    }

    [Fact]
    public void IntegersAreNeverRounded()
    {
        Display(42).ShouldBe("42");
        Display(9007199254740993L).ShouldBe("9007199254740993");
        Display((short)-7).ShouldBe("-7");
    }

    [Fact]
    public void DecimalsAreExact_SoTheyShowAsTheDatabaseSentThem()
    {
        Display(259.9700m).ShouldBe("259.9700");
        Display(0.30000000000000004m).ShouldBe("0.30000000000000004");
    }

    [Fact]
    public void Null_HasNoText()
    {
        Display(null).ShouldBeNull();
    }

    [Fact]
    public void Booleans_AreLowercase()
    {
        Display(true).ShouldBe("true");
        Display(false).ShouldBe("false");
    }

    [Fact]
    public void DateTime_ShowsDateAndTime_WithFractionOnlyWhenPresent()
    {
        Display(new DateTime(2024, 9, 1)).ShouldBe("2024-09-01 00:00:00");
        Display(new DateTime(2024, 9, 1, 13, 5, 7, 250)).ShouldBe("2024-09-01 13:05:07.25");
    }

    [Theory]
    [InlineData("date")]
    [InlineData("DATE")]
    public void DateTime_InADateColumn_ShowsOnlyTheDate(string type)
    {
        Display(new DateTime(2024, 9, 1), type).ShouldBe("2024-09-01");
    }

    [Fact]
    public void OtherDateAndTimeTypes_UseTheSameShapes()
    {
        Display(new DateTimeOffset(2024, 9, 1, 13, 5, 7, TimeSpan.FromHours(2))).ShouldBe("2024-09-01 13:05:07 +02:00");
        Display(new DateOnly(2024, 9, 1)).ShouldBe("2024-09-01");
        Display(new TimeOnly(13, 5, 7)).ShouldBe("13:05:07");
        Display(new TimeSpan(1, 2, 3)).ShouldBe("01:02:03");
    }

    [Fact]
    public void Binary_ShowsHex_AndShortensLongValues()
    {
        Display(new byte[] { 0x0A, 0xFF }).ShouldBe("0x0AFF");
        Display(Enumerable.Repeat((byte)0xAB, 40).ToArray()).ShouldBe("0x" + string.Concat(Enumerable.Repeat("AB", 32)) + "… (40 bytes)");
    }

    [Fact]
    public void Text_IsShownAsIs()
    {
        Display("  spaced  ").ShouldBe("  spaced  ");
    }

    [Fact]
    public void Full_KeepsEveryDigit_ForTitlesAndCopies()
    {
        ResultValueFormatter.Full(259.96999999999997, Invariant).ShouldBe("259.96999999999997");
        ResultValueFormatter.Full(259.9700m, Invariant).ShouldBe("259.9700");
        ResultValueFormatter.Full(null, Invariant).ShouldBeNull();
    }

    [Theory]
    [InlineData("integer")]
    [InlineData("INTEGER")]
    [InlineData("bigint")]
    [InlineData("int4")]
    [InlineData("INT UNSIGNED")]
    [InlineData("real")]
    [InlineData("double precision")]
    [InlineData("float8")]
    [InlineData("numeric(10,2)")]
    [InlineData("decimal")]
    [InlineData("money")]
    [InlineData("tinyint")]
    public void IsNumericType_RecognizesNumberTypes(string type)
    {
        ResultValueFormatter.IsNumericType(type).ShouldBeTrue();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("varchar(20)")]
    [InlineData("date")]
    [InlineData("boolean")]
    [InlineData("bit")]
    [InlineData("interval")]
    [InlineData("")]
    [InlineData(null)]
    public void IsNumericType_RejectsOtherTypes(string? type)
    {
        ResultValueFormatter.IsNumericType(type).ShouldBeFalse();
    }

    [Fact]
    public void IsNumericColumn_TrustsTheValuesWhenThereAreAny()
    {
        ResultValueFormatter.IsNumericColumn("text", [1L, 2.5, null]).ShouldBeTrue();
        ResultValueFormatter.IsNumericColumn("tinyint", [true, false]).ShouldBeFalse();
        ResultValueFormatter.IsNumericColumn(null, ["a", 1L]).ShouldBeFalse();
    }

    [Fact]
    public void IsNumericColumn_AcceptsNumberTextInANumberTypedColumn()
    {
        // PGlite sends numeric values as text so no digits are lost.
        ResultValueFormatter.IsNumericColumn("numeric", ["259.97", "10"]).ShouldBeTrue();
        ResultValueFormatter.IsNumericColumn("text", ["259.97", "10"]).ShouldBeFalse();
    }

    [Fact]
    public void IsNumericColumn_WithOnlyNulls_FollowsTheType()
    {
        ResultValueFormatter.IsNumericColumn("integer", [null, null]).ShouldBeTrue();
        ResultValueFormatter.IsNumericColumn(null, [null]).ShouldBeFalse();
    }
}
