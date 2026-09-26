using System.Globalization;
using Aion.Components.Querying.Editing;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class CellEditTextTests
{
    [Fact]
    public void From_Null_IsNull()
    {
        CellEditText.From(null).ShouldBeNull();
        CellEditText.From(DBNull.Value).ShouldBeNull();
    }

    [Fact]
    public void From_Numbers_KeepEveryDigitAndIgnoreTheCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            CellEditText.From(259.96999999999997).ShouldBe("259.96999999999997");
            CellEditText.From(1234.5m).ShouldBe("1234.5");
            CellEditText.From(150L).ShouldBe("150");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void From_DatesAndBooleans_UseTheTextSqlReads()
    {
        CellEditText.From(new DateTime(2024, 12, 31, 8, 30, 0)).ShouldBe("2024-12-31 08:30:00");
        CellEditText.From(new DateTime(2024, 12, 31), "date").ShouldBe("2024-12-31");
        CellEditText.From(true).ShouldBe("true");
    }

    [Fact]
    public void CanEdit_IsFalseForBinaryValues()
    {
        CellEditText.CanEdit(new byte[] { 1, 2 }).ShouldBeFalse();
        CellEditText.CanEdit("text").ShouldBeTrue();
        CellEditText.CanEdit(null).ShouldBeTrue();
    }

    [Fact]
    public void Resolve_TextThatStillReadsAsTheOriginal_KeepsTheOriginalValue()
    {
        object original = 150L;

        CellEditText.Resolve("150", original).ShouldBeSameAs(original);
    }

    [Fact]
    public void Resolve_NullForANullOriginal_IsNull()
    {
        CellEditText.Resolve(null, null).ShouldBeNull();
    }

    [Fact]
    public void Resolve_ChangedText_IsTheText()
    {
        CellEditText.Resolve("151", 150L).ShouldBe("151");
    }

    [Fact]
    public void Resolve_EmptyText_IsAnEmptyStringNotNull()
    {
        CellEditText.Resolve("", "USB-C Hub").ShouldBe("");
    }

    [Fact]
    public void Resolve_Null_IsNullEvenWhenTheOriginalHadAValue()
    {
        CellEditText.Resolve(null, "USB-C Hub").ShouldBeNull();
    }
}
