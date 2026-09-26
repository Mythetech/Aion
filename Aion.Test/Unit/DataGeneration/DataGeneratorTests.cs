using Aion.Components.Scaffolding;
using Aion.Components.Scaffolding.DataGeneration;
using Aion.Contracts.Database;
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

        gen.Generate(0, options).ShouldBe(10L);
        gen.Generate(1, options).ShouldBe(11L);
        gen.Generate(5, options).ShouldBe(15L);
    }

    [Fact]
    public void AutoIncrement_DefaultStartIsOne()
    {
        var gen = new AutoIncrementGenerator();

        gen.Generate(0, _defaultOptions).ShouldBe(1L);
        gen.Generate(2, _defaultOptions).ShouldBe(3L);
    }

    [Fact]
    public void AutoIncrement_ContinuesAfterTheHighestExistingValue()
    {
        var gen = new AutoIncrementGenerator();
        var options = new DataGeneratorOptions { ExistingMaximum = 41 };

        gen.Generate(0, options).ShouldBe(42L);
        gen.Generate(1, options).ShouldBe(43L);
    }

    [Fact]
    public void RandomInt_ShouldRespectRange()
    {
        var gen = new RandomIntGenerator();
        var options = new DataGeneratorOptions { MinValue = 50, MaxValue = 60 };

        for (int i = 0; i < 100; i++)
        {
            var value = (long)gen.Generate(i, options)!;
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
            var date = (DateTime)gen.Generate(i, options)!;
            date.ShouldBeGreaterThanOrEqualTo(new DateTime(2023, 1, 1));
            date.Date.ShouldBeLessThanOrEqualTo(new DateTime(2023, 12, 31));
            date.Millisecond.ShouldBe(0);
        }
    }

    [Fact]
    public void UuidGenerator_ShouldReturnValidGuid()
    {
        var gen = new UuidGenerator();

        for (int i = 0; i < 10; i++)
        {
            gen.Generate(i, _defaultOptions).ShouldBeOfType<Guid>().ShouldNotBe(Guid.Empty);
        }
    }

    [Fact]
    public void UuidGenerator_ShouldReturnUniqueValues()
    {
        var gen = new UuidGenerator();
        var values = Enumerable.Range(0, 100)
            .Select(i => (Guid)gen.Generate(i, _defaultOptions)!)
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
    [InlineData("int4", typeof(RandomIntGenerator))]
    [InlineData("numeric", typeof(RandomNumberGenerator))]
    [InlineData("text", typeof(RandomTextGenerator))]
    [InlineData("character varying(20)", typeof(NameGenerator))]
    [InlineData("boolean", typeof(BooleanGenerator))]
    [InlineData("integer", typeof(BooleanGenerator))]
    [InlineData("uuid", typeof(UuidGenerator))]
    [InlineData("date", typeof(DateRangeGenerator))]
    [InlineData("timestamp with time zone", typeof(DateRangeGenerator))]
    [InlineData("time", typeof(DateRangeGenerator))]
    [InlineData("jsonb", typeof(JsonGenerator))]
    public void Supports_TheTypesItCanFill(string dataType, Type generatorType)
    {
        var generator = DataGenerators.All.First(g => g.GetType() == generatorType);

        generator.Supports(ColumnTypeShape.Of(dataType, DatabaseType.PostgreSQL)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("interval", typeof(DateRangeGenerator))]
    [InlineData("date", typeof(RandomTextGenerator))]
    [InlineData("boolean", typeof(RandomIntGenerator))]
    [InlineData("bytea", typeof(RandomTextGenerator))]
    [InlineData("text", typeof(BooleanGenerator))]
    public void Supports_RefusesTypesItWouldWriteInvalidValuesInto(string dataType, Type generatorType)
    {
        var generator = DataGenerators.All.First(g => g.GetType() == generatorType);

        generator.Supports(ColumnTypeShape.Of(dataType, DatabaseType.PostgreSQL)).ShouldBeFalse();
    }

    [Fact]
    public void ReferencedValue_PicksFromTheValuesItWasGiven()
    {
        var gen = new ReferencedValueGenerator();
        var options = new DataGeneratorOptions { ReferencedValues = [7L, 9L] };

        for (int i = 0; i < 20; i++)
            gen.Generate(i, options).ShouldBeOneOf(7L, 9L);
    }

    [Fact]
    public void RandomNumber_KeepsTwoDecimalPlacesWithinTheRange()
    {
        var gen = new RandomNumberGenerator();
        var options = new DataGeneratorOptions { MinValue = 1, MaxValue = 5 };

        for (int i = 0; i < 50; i++)
        {
            var value = (decimal)gen.Generate(i, options)!;
            value.ShouldBeInRange(1m, 5m);
            decimal.Round(value, 2).ShouldBe(value);
        }
    }
}
