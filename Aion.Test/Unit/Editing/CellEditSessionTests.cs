using Aion.Components.Querying.Editing;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class CellEditSessionTests
{
    private static readonly EditableColumn Editable = new(true, null);

    [Fact]
    public void TypedText_IsWhatCommits()
    {
        var session = new CellEditSession(0, "name", "Ada", Editable);

        session.Type("Ada Lovelace");

        session.CommitText.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void EmptyText_CommitsNull()
    {
        var session = new CellEditSession(0, "name", "Ada", Editable);

        session.Type("");

        session.CommitText.ShouldBeNull();
    }
}
