using Aion.Components.Querying;
using Shouldly;

namespace Aion.Test.Unit;

public class SelectStarExpanderTests
{
    [Fact]
    public void TryParse_SimpleSelect_ReturnsTableName()
    {
        var result = SelectStarExpander.TryParse("SELECT * FROM users", out var schema, out var table);

        result.ShouldBeTrue();
        schema.ShouldBeNull();
        table.ShouldBe("users");
    }

    [Fact]
    public void TryParse_CaseInsensitive_ReturnsTableName()
    {
        var result = SelectStarExpander.TryParse("select * from Orders", out var schema, out var table);

        result.ShouldBeTrue();
        schema.ShouldBeNull();
        table.ShouldBe("Orders");
    }

    [Fact]
    public void TryParse_SchemaQualified_ReturnsBoth()
    {
        var result = SelectStarExpander.TryParse("SELECT * FROM public.users", out var schema, out var table);

        result.ShouldBeTrue();
        schema.ShouldBe("public");
        table.ShouldBe("users");
    }

    [Fact]
    public void TryParse_QuotedTable_ReturnsTableName()
    {
        var result = SelectStarExpander.TryParse("SELECT * FROM \"users\"", out var schema, out var table);

        result.ShouldBeTrue();
        schema.ShouldBeNull();
        table.ShouldBe("users");
    }

    [Fact]
    public void TryParse_BracketedTable_ReturnsTableName()
    {
        var result = SelectStarExpander.TryParse("SELECT * FROM [users]", out var schema, out var table);

        result.ShouldBeTrue();
        schema.ShouldBeNull();
        table.ShouldBe("users");
    }

    [Fact]
    public void TryParse_NoMatch_ReturnsFalse()
    {
        var result = SelectStarExpander.TryParse("SELECT name FROM users", out var schema, out var table);

        result.ShouldBeFalse();
        schema.ShouldBeNull();
        table.ShouldBeNull();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var result = SelectStarExpander.TryParse("", out var schema, out var table);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryParse_SelectStarNoFrom_ReturnsFalse()
    {
        var result = SelectStarExpander.TryParse("SELECT *", out var schema, out var table);

        result.ShouldBeFalse();
    }

    [Fact]
    public void BuildExpandedSelect_SimpleTable_FormatsColumns()
    {
        var columns = new List<string> { "id", "name", "email" };

        var result = SelectStarExpander.BuildExpandedSelect(columns, null, "users");

        result.ShouldBe("SELECT id, name, email FROM users");
    }

    [Fact]
    public void BuildExpandedSelect_SchemaQualified_IncludesSchema()
    {
        var columns = new List<string> { "id", "name" };

        var result = SelectStarExpander.BuildExpandedSelect(columns, "public", "users");

        result.ShouldBe("SELECT id, name FROM public.users");
    }

    [Fact]
    public void BuildExpandedSelect_SingleColumn_NoTrailingComma()
    {
        var columns = new List<string> { "id" };

        var result = SelectStarExpander.BuildExpandedSelect(columns, null, "users");

        result.ShouldBe("SELECT id FROM users");
    }
}
