using Aion.Components.Scaffolding.DataGeneration;
using Shouldly;

namespace Aion.Test.Unit.DataGeneration;

public class DataGeneratorTests
{
    private readonly DataGeneratorOptions _defaultOptions = new();

    [Fact]
    public void AutoIncrement_ShouldGenerateSequentialValues()
    {
        var gen = new AutoIncrementGenerator();
        var options = new DataGeneratorOptions { StartValue = 10 };

        gen.Generate(0, options).ShouldBe(10);
        gen.Generate(1, options).ShouldBe(11);
        gen.Generate(5, options).ShouldBe(15);
    }

    [Fact]
    public void AutoIncrement_DefaultStartIsOne()
    {
        var gen = new AutoIncrementGenerator();

        gen.Generate(0, _defaultOptions).ShouldBe(1);
        gen.Generate(2, _defaultOptions).ShouldBe(3);
    }

    [Fact]
    public void RandomInt_ShouldRespectRange()
    {
        var gen = new RandomIntGenerator();
        var options = new DataGeneratorOptions { MinValue = 50, MaxValue = 60 };

        for (int i = 0; i < 100; i++)
        {
            var value = (int)gen.Generate(i, options)!;
            value.ShouldBeGreaterThanOrEqualTo(50);
            value.ShouldBeLessThanOrEqualTo(60);
        }
    }

    [Fact]
    public void RandomText_ShouldRespectLengthBounds()
    {
        var gen = new RandomTextGenerator();
        var options = new DataGeneratorOptions { MinLength = 3, MaxLength = 8 };

        for (int i = 0; i < 50; i++)
        {
            var value = (string)gen.Generate(i, options)!;
            value.Length.ShouldBeGreaterThanOrEqualTo(3);
            value.Length.ShouldBeLessThanOrEqualTo(8);
        }
    }

    [Fact]
    public void RandomText_ShouldContainOnlyAlphaChars()
    {
        var gen = new RandomTextGenerator();

        for (int i = 0; i < 50; i++)
        {
            var value = (string)gen.Generate(i, _defaultOptions)!;
            value.ShouldAllBe(c => c >= 'a' && c <= 'z');
        }
    }

    [Fact]
    public void NameGenerator_ShouldReturnFirstAndLastName()
    {
        var gen = new NameGenerator();

        for (int i = 0; i < 20; i++)
        {
            var value = (string)gen.Generate(i, _defaultOptions)!;
            value.ShouldContain(" ");
            value.Split(' ').Length.ShouldBe(2);
        }
    }

    [Fact]
    public void EmailGenerator_ShouldReturnValidEmailFormat()
    {
        var gen = new EmailGenerator();

        for (int i = 0; i < 20; i++)
        {
            var value = (string)gen.Generate(i, _defaultOptions)!;
            value.ShouldContain("@");
            value.ShouldContain(".");
            value.Split('@').Length.ShouldBe(2);
        }
    }

    [Fact]
    public void DateRange_ShouldRespectBounds()
    {
        var gen = new DateRangeGenerator();
        var options = new DataGeneratorOptions
        {
            MinDate = new DateTime(2023, 1, 1),
            MaxDate = new DateTime(2023, 12, 31)
        };

        for (int i = 0; i < 50; i++)
        {
            var value = (string)gen.Generate(i, options)!;
            var date = DateTime.Parse(value);
            date.ShouldBeGreaterThanOrEqualTo(new DateTime(2023, 1, 1));
            date.ShouldBeLessThanOrEqualTo(new DateTime(2023, 12, 31));
        }
    }

    [Fact]
    public void UuidGenerator_ShouldReturnValidGuid()
    {
        var gen = new UuidGenerator();

        for (int i = 0; i < 10; i++)
        {
            var value = (string)gen.Generate(i, _defaultOptions)!;
            Guid.TryParse(value, out _).ShouldBeTrue();
        }
    }

    [Fact]
    public void UuidGenerator_ShouldReturnUniqueValues()
    {
        var gen = new UuidGenerator();
        var values = Enumerable.Range(0, 100)
            .Select(i => (string)gen.Generate(i, _defaultOptions)!)
            .ToHashSet();

        values.Count.ShouldBe(100);
    }

    [Fact]
    public void BooleanGenerator_ShouldReturnBoolValues()
    {
        var gen = new BooleanGenerator();

        for (int i = 0; i < 20; i++)
        {
            var value = gen.Generate(i, _defaultOptions);
            value.ShouldBeOfType<bool>();
        }
    }

    [Fact]
    public void CustomList_ShouldPickFromProvidedValues()
    {
        var gen = new CustomListGenerator();
        var options = new DataGeneratorOptions { CustomValues = "red, green, blue" };
        var allowed = new[] { "red", "green", "blue" };

        for (int i = 0; i < 50; i++)
        {
            var value = (string)gen.Generate(i, options)!;
            allowed.ShouldContain(value);
        }
    }

    [Fact]
    public void CustomList_EmptyValues_ShouldReturnNull()
    {
        var gen = new CustomListGenerator();
        var options = new DataGeneratorOptions { CustomValues = "" };

        gen.Generate(0, options).ShouldBeNull();
    }

    [Fact]
    public void NullGenerator_ShouldAlwaysReturnNull()
    {
        var gen = new NullGenerator();

        for (int i = 0; i < 10; i++)
            gen.Generate(i, _defaultOptions).ShouldBeNull();
    }

    [Theory]
    [InlineData("integer", typeof(AutoIncrementGenerator))]
    [InlineData("text", typeof(RandomTextGenerator))]
    [InlineData("boolean", typeof(BooleanGenerator))]
    [InlineData("uuid", typeof(UuidGenerator))]
    [InlineData("date", typeof(DateRangeGenerator))]
    [InlineData("timestamp", typeof(DateRangeGenerator))]
    public void SupportsType_ShouldMatchExpectedGenerators(string dataType, Type expectedGeneratorType)
    {
        var generator = DataGenerators.All.First(g => g.GetType() == expectedGeneratorType);
        generator.SupportsType(dataType).ShouldBeTrue();
    }

    [Theory]
    [InlineData("email", "text", typeof(EmailGenerator))]
    [InlineData("user_email", "varchar", typeof(EmailGenerator))]
    [InlineData("first_name", "text", typeof(NameGenerator))]
    [InlineData("full_name", "varchar", typeof(NameGenerator))]
    [InlineData("uuid", "uuid", typeof(UuidGenerator))]
    [InlineData("created_at", "timestamp", typeof(DateRangeGenerator))]
    [InlineData("is_active", "boolean", typeof(BooleanGenerator))]
    public void SuggestGenerator_ShouldMatchColumnNameHeuristics(string columnName, string dataType, Type expectedType)
    {
        var result = DataGenerators.SuggestGenerator(columnName, dataType, isIdentity: false);
        result.ShouldNotBeNull();
        result.GetType().ShouldBe(expectedType);
    }

    [Fact]
    public void SuggestGenerator_IdentityColumn_ShouldReturnNull()
    {
        DataGenerators.SuggestGenerator("id", "integer", isIdentity: true).ShouldBeNull();
    }
}
