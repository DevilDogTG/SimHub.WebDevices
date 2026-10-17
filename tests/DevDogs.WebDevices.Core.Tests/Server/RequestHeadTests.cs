using DevDogs.WebDevices.Core.Server;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Server;

public class RequestHeadTests
{
    [Fact]
    public void Parses_method_and_path()
    {
        RequestHead.TryParse("GET /ipad/ HTTP/1.1\r\nHost: pc:8889\r\n\r\n", out var head).Should().BeTrue();

        head.Should().Be(new RequestHead("GET", "/ipad/"));
    }

    [Theory]
    [InlineData("GET /ipad/state?t=123 HTTP/1.1", "/ipad/state")]
    [InlineData("GET /ipad/#x HTTP/1.1", "/ipad/")]
    [InlineData("get /ipad/ HTTP/1.0", "/ipad/")]
    public void Drops_query_and_fragment_and_normalizes_the_method(string requestLine, string path)
    {
        RequestHead.TryParse(requestLine + "\r\n\r\n", out var head).Should().BeTrue();

        head!.Method.Should().Be("GET");
        head.Path.Should().Be(path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("GET /ipad/")]
    [InlineData("GET ipad HTTP/1.1")]
    [InlineData("GET /ipad/ SPDY/3")]
    [InlineData("GET http://pc/ipad/ HTTP/1.1")]
    public void Rejects_malformed_request_lines(string requestLine)
    {
        RequestHead.TryParse(requestLine + "\r\n\r\n", out _).Should().BeFalse();
    }
}
