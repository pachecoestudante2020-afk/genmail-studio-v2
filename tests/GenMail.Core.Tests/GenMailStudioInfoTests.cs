using Xunit;
using GenMail.Core;

namespace GenMail.Core.Tests;

public class GenMailStudioInfoTests
{
    [Fact]
    public void ProductName_ReturnsExpectedValue()
    {
        Assert.Equal("GenMail Studio", GenMailStudioInfo.ProductName);
    }
}
