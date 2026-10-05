using System.Text.Json;
using Splinter.Runtime;

namespace Splinter.Tests;

public sealed class JsonPropertyValidationTests
{
    [Theory]
    [InlineData("{\"name\":1,\"name\":2}", false)]
    [InlineData("{\"items\":[{\"name\":1,\"name\":2}]}", false)]
    [InlineData("[{\"nested\":{\"name\":1,\"na\\u006de\":2}}]", false)]
    [InlineData("{\"name\":1,\"Name\":2,\"items\":[{\"name\":3},{\"name\":4}]}", true)]
    [InlineData("[null,1,\"name\",true,{},[]]", true)]
    public void RejectsAmbiguousNamesAtAnyDepthButKeepsOrdinalObjectScope(string json, bool unique)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(unique, JsonPropertyValidation.HasUniqueProperties(document.RootElement));
    }
}
