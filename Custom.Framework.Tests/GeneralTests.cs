using FluentAssertions;
using Custom.Framework.Extensions;
using Custom.Framework.TestFactory.Core;
using Xunit.Abstractions;

namespace Custom.Framework.Tests;

public class GeneralTests(ITestOutputHelper output) : TestHostBase(output)
{
    [Fact]
    public void GetValueOrDefault_String_Test()
    {
        var dictionary = new Dictionary<string, string>
                {
                    { "key1", "value1" },
                    { "KEY11", "value11" },
                    { "mobileOnly1", "True" },
                    { "mobileonly2", "Y" },
                    { "MOBILEONLY3", "y" },
                    { "mobileOnly11", "False" },
                    { "mobileOnly22", "F" },
                    { "mobileOnly33", "f" },
                    { "key3", "0" }
                };

        var result = dictionary.GetValueOrDefault("key1", "value1");
        result.Should().Be("value1");
        result = dictionary.GetValueOrDefault("key11", "value1");
        result.Should().Be("value11");

        var resultBool = dictionary.GetValueOrDefault<bool>("mobileOnly1", default);
        resultBool.Should().Be(true);
        resultBool = dictionary.GetValueOrDefault("mobileonly2", false);
        resultBool.Should().Be(true);
        resultBool = dictionary.GetValueOrDefault("MOBILEONLY3", false);
        resultBool.Should().Be(true);

        resultBool = dictionary.GetValueOrDefault("mobileOnly11", false);
        resultBool.Should().Be(false);
        resultBool = dictionary.GetValueOrDefault("mobileonly22", false);
        resultBool.Should().Be(false);
        resultBool = dictionary.GetValueOrDefault("MOBILEONLY33", false);
        resultBool.Should().Be(false);

        var resultInt = dictionary.GetValueOrDefault("key5", 0);
        resultInt.Should().Be(0);
    }
}