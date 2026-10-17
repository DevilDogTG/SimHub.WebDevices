using DevDogs.WebDevices.Core.Server;
using FluentAssertions;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Server;

public class EndpointRoutesTests
{
    private static Route Resolve(string method, string path) =>
        EndpointRoutes.Resolve(new RequestHead(method, path), name => name is "ipad" or "s8");

    [Theory]
    [InlineData("/ipad/", RouteKind.Page)]
    [InlineData("/ipad/events", RouteKind.Events)]
    [InlineData("/ipad/state", RouteKind.State)]
    [InlineData("/ipad", RouteKind.Redirect)]
    public void Known_device_routes(string path, RouteKind kind)
    {
        Resolve("GET", path).Should().Be(new Route(kind, "ipad"));
    }

    [Fact]
    public void Device_names_in_the_url_are_matched_ignoring_case()
    {
        Resolve("GET", "/iPad/").Should().Be(new Route(RouteKind.Page, "ipad"));
    }

    [Fact]
    public void Head_is_served_like_get()
    {
        Resolve("HEAD", "/ipad/").Should().Be(new Route(RouteKind.Page, "ipad"));
    }

    [Theory]
    [InlineData("/phone/")]
    [InlineData("/phone")]
    [InlineData("/phone/events")]
    [InlineData("/phone/state")]
    public void Unknown_devices_are_not_configured(string path)
    {
        Resolve("GET", path).Should().Be(new Route(RouteKind.DeviceNotConfigured, "phone"));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/favicon.ico")]
    [InlineData("/ipad/other")]
    [InlineData("/ipad/events/")]
    [InlineData("/ipad//")]
    [InlineData("/a/b/c")]
    [InlineData("/Bad Name/")]
    public void Anything_else_is_not_found(string path)
    {
        Resolve("GET", path).Should().Be(new Route(RouteKind.NotFound, null));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public void Only_get_and_head_are_allowed(string method)
    {
        Resolve(method, "/ipad/").Should().Be(new Route(RouteKind.MethodNotAllowed, null));
    }
}
