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
        new() { Name = "name", DataType = "character varying", IsNullable = false }
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
    public void OtherColumns_AreEditable_WhateverTheirCase()
    {
        var editable = EditableColumn.For("NAME", Columns);

        editable.IsEditable.ShouldBeTrue();
        editable.ReadOnlyReason.ShouldBeNull();
    }
}
