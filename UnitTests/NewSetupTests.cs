using CustomBossChallenge.Config;
using CustomBossChallenge.Persistence;
using Newtonsoft.Json;
using Xunit;

namespace CustomBossChallenge.UnitTests;

/// <summary>
/// A new setup has Boss Event Rounds on (#251), while codes and files keep comparing with the defaults they were
/// written against, Boss Event Rounds off, so they keep their rounds
/// </summary>
public class NewSetupTests
{
    [Fact]
    public void A_new_setup_plays_the_boss_events_rounds()
    {
        Assert.True(BossOptions.NewSetup.BossEventRounds);
        Assert.Equal(new BossOptions() with {BossEventRounds = true}, BossOptions.NewSetup);
    }

    [Fact]
    public void Codes_and_files_compare_with_Boss_Event_Rounds_off()
    {
        Assert.False(new BossOptions().BossEventRounds);
        // A file or code without the option, from before it existed, plays the normal rounds
        Assert.False(JsonConvert.DeserializeObject<BossOptions>("{\"Boss\": \"Lych\"}")!.BossEventRounds);
    }

    [Fact]
    public void A_new_setup_in_a_code_names_its_event_rounds()
    {
        var options = JsonConvert.SerializeObject(BossOptions.NewSetup);
        var defaults = JsonConvert.SerializeObject(new BossOptions());
        var payload = CodeFormat.Payload(Newtonsoft.Json.Linq.JObject.Parse(options), Newtonsoft.Json.Linq.JObject.Parse(defaults),
            new Newtonsoft.Json.Linq.JObject(), new Newtonsoft.Json.Linq.JObject());
        Assert.Equal(true, (bool?) payload["o"]!["BossEventRounds"]);
    }
}
