using System.Text.Json.Nodes;
using Xunit;
using Zero.Emulation.Settings;

namespace Zero.Core.Tests
{
    // Flash loading replaced fast loading: a setting saved before it carries over.
    public class TapeSettingsTests
    {
        [Fact]
        public void Fast_loading_saved_before_turns_flash_loading_on()
        {
            var json = JsonNode.Parse("{ \"Tape\": { \"FastLoad\": true } }");
            Assert.True(EmulatorSettings.FromJson(json).Tape.FlashLoad);
        }

        [Fact]
        public void Flash_loading_is_saved_under_its_own_name_and_fast_loading_is_not()
        {
            var settings = new EmulatorSettings();
            settings.Tape.FlashLoad = true;
            JsonObject tape = settings.ToJson()["Tape"]!.AsObject();
            Assert.True(tape["FlashLoad"]!.GetValue<bool>());
            Assert.False(tape.ContainsKey("FastLoad"));
            Assert.True(EmulatorSettings.FromJson(settings.ToJson()).Tape.FlashLoad);
        }
    }
}
