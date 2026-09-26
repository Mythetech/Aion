using Aion.Components.Querying.Editing;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class CellEditSessionTests
{
    private static readonly EditableColumn NullableText = new(true, IsNullable: true, AcceptsEmptyText: true, null);
    private static readonly EditableColumn RequiredText = new(true, IsNullable: false, AcceptsEmptyText: true, null);
    private static readonly EditableColumn NullableNumber = new(true, IsNullable: true, AcceptsEmptyText: false, null);
    private static readonly EditableColumn RequiredNumber = new(true, IsNullable: false, AcceptsEmptyText: false, null);

    [Fact]
    public void EmptyText_InATextColumn_CommitsAnEmptyString()
    {
        var session = new CellEditSession(0, "note", "hello", NullableText);
        session.Type("");

        session.TryGetCommitText(out var text).ShouldBeTrue();
        text.ShouldBe("");
        session.ShowsNull.ShouldBeFalse();
    }

    [Fact]
    public void SetNull_InANullableColumn_CommitsNull()
    {
        var session = new CellEditSession(0, "note", "hello", NullableText);

        session.SetNull().ShouldBeTrue();

        session.TryGetCommitText(out var text).ShouldBeTrue();
        text.ShouldBeNull();
        session.ShowsNull.ShouldBeTrue();
    }

    [Fact]
    public void SetNull_InANonNullableColumn_KeepsTheText()
    {
        var session = new CellEditSession(0, "name", "hello", RequiredText);

        session.SetNull().ShouldBeFalse();

        session.Text.ShouldBe("hello");
    }

    [Fact]
    public void EmptyText_InANullableNumberColumn_CommitsNull()
    {
        var session = new CellEditSession(0, "price", "9.99", NullableNumber);
        session.Type("");

        session.TryGetCommitText(out var text).ShouldBeTrue();
        text.ShouldBeNull();
        session.ShowsNull.ShouldBeTrue();
    }

    [Fact]
    public void EmptyText_InARequiredNumberColumn_CannotBeCommitted()
    {
        var session = new CellEditSession(0, "stock", "150", RequiredNumber);
        session.Type("");

        session.TryGetCommitText(out _).ShouldBeFalse();
    }

    [Fact]
    public void TypingIntoANullCell_LeavesNull()
    {
        var session = new CellEditSession(0, "note", null, NullableText);
        session.ShowsNull.ShouldBeTrue();

        session.Type("x");

        session.ShowsNull.ShouldBeFalse();
        session.Text.ShouldBe("x");
    }
}
