using Aion.Components.Querying.Editing;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class EditableColumnTests
{
    private static readonly List<ColumnInfo> Columns =
    [
        new() { Name = "id", DataType = "integer", IsPrimaryKey = true, IsIdentity = true },
        new() { Name = "code", DataType = "varchar(20)", IsPrimaryKey = true },
        new() { Name = "row_version", DataType = "bigint", IsIdentity = true },
        new() { Name = "name", DataType = "character varying", IsNullable = false },
        new() { Name = "note", DataType = "TEXT", IsNullable = true },
        new() { Name = "price", DataType = "numeric(10,2)", IsNullable = true },
        new() { Name = "stock", DataType = "integer", IsNullable = false },
        new() { Name = "anything", DataType = "", IsNullable = true }
    ];

    [Theory]
    [InlineData("id")]
    [InlineData("code")]
    [InlineData("row_version")]
    [InlineData("computed")]
    public void KeysIdentitiesAndUnknownColumns_AreNotEditable(string column)
    {
        var editable = EditableColumn.For(column, Columns);

        editable.IsEditable.ShouldBeFalse();
        editable.ReadOnlyReason.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Lookup_IgnoresCase()
    {
        EditableColumn.For("NOTE", Columns).IsNullable.ShouldBeTrue();
    }

    [Theory]
    [InlineData("name", false, true)]
    [InlineData("note", true, true)]
    [InlineData("price", true, false)]
    [InlineData("stock", false, false)]
    [InlineData("anything", true, true)]
    public void EditableColumns_SayWhetherTheyTakeNullAndEmptyText(string column, bool nullable, bool acceptsEmptyText)
    {
        var editable = EditableColumn.For(column, Columns);

        editable.IsEditable.ShouldBeTrue();
        editable.IsNullable.ShouldBe(nullable);
        editable.AcceptsEmptyText.ShouldBe(acceptsEmptyText);
    }
}
