using Aion.Components.Querying.Errors;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class IdentifierMatcherTests
{
    private static readonly string[] ProductColumns = ["id", "name", "price", "category_id", "created_at"];

    [Theory]
    [InlineData("categry_id", "category_id")]
    [InlineData("catgeory_id", "category_id")]
    [InlineData("nme", "name")]
    [InlineData("prise", "price")]
    [InlineData("CATEGORY_ID", "category_id")]
    [InlineData("Name", "name")]
    public void Closest_FindsTheNameATypoMeant(string typo, string expected)
    {
        // Act
        var match = IdentifierMatcher.Closest(typo, ProductColumns);

        // Assert
        match.ShouldBe(expected);
    }

    [Theory]
    [InlineData("weight")]
    [InlineData("category")]
    [InlineData("xy")]
    [InlineData("")]
    public void Closest_WithoutACloseName_SuggestsNothing(string name)
    {
        // Act
        var match = IdentifierMatcher.Closest(name, ProductColumns);

        // Assert
        match.ShouldBeNull();
    }

    [Fact]
    public void Closest_NeverSuggestsTheNameItself()
    {
        // Act
        var match = IdentifierMatcher.Closest("price", ["price", "prices"]);

        // Assert
        match.ShouldBe("prices");
    }

    [Fact]
    public void Closest_PrefersTheSmallestDistance()
    {
        // Act
        var match = IdentifierMatcher.Closest("custmer_name", ["customer_name", "customer_names"]);

        // Assert
        match.ShouldBe("customer_name");
    }

    [Fact]
    public void Closest_BreaksTiesTheSameWayEveryTime()
    {
        // Act
        var first = IdentifierMatcher.Closest("cat", ["car", "bat"]);
        var second = IdentifierMatcher.Closest("cat", ["bat", "car"]);

        // Assert
        first.ShouldBe("bat");
        second.ShouldBe("bat");
    }

    [Fact]
    public void Closest_WithShortNames_OnlyMatchesADifferentCase()
    {
        // Act
        var caseOnly = IdentifierMatcher.Closest("ID", ["id", "ix"]);
        var typo = IdentifierMatcher.Closest("ic", ["id"]);

        // Assert
        caseOnly.ShouldBe("id");
        typo.ShouldBeNull();
    }
}
