using DevDogs.WebDevices.Core.Routing;
using DevDogs.WebDevices.Core.Server;
using DevDogs.WebDevices.Core.State;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DevDogs.WebDevices.Core.Tests.Server;

public class DevicePayloadTests
{
    [Fact]
    public void Carries_everything_the_device_page_needs_on_one_line()
    {
        var json = DevicePayload.ToJson(
            "ipad",
            new RouterState(GameState.Driving, "ETS2"),
            new DeviceTarget("Truck", "/Dash?v=1#Truck"),
            simHubWebPort: 8888);

        json.Should().NotContain("\n");
        JObject.Parse(json).Should().BeEquivalentTo(JObject.Parse("""
            {
              "device": "ipad",
              "state": "Driving",
              "game": "ETS2",
              "dashboard": "Truck",
              "path": "/Dash?v=1#Truck",
              "simHubPort": 8888
            }
            """));
    }

    [Fact]
    public void Missing_values_are_explicit_nulls()
    {
        var json = DevicePayload.ToJson("ipad", RouterState.SystemIdle, new DeviceTarget(null, null), 8888);

        var payload = JObject.Parse(json);
        payload["state"]!.Value<string>().Should().Be("SystemIdle");
        payload["game"]!.Type.Should().Be(JTokenType.Null);
        payload["dashboard"]!.Type.Should().Be(JTokenType.Null);
        payload["path"]!.Type.Should().Be(JTokenType.Null);
    }
}
