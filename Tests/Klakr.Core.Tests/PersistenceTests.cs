using System.IO;

namespace Klakr.Core.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "klakr-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static Profile SampleProfile() => new()
    {
        Name = "Combat",
        Hotkey = new Hotkey(Key.F13),
        DefaultKeyDelay = new DelayRange(80, 120),
        RootStep = new LoopStep
        {
            Iterations = null,
            Ordering = SequenceType.Priority,
            Children =
            [
                new KeyTapStep
                {
                    Key = Key.W,
                    HoldMinMs = 30,
                    HoldMaxMs = 50,
                    DelayAfter = new DelayRange(200, 260),
                },
                new DelayStep { MinMs = 100, MaxMs = 150 },
                new ConditionalBranchStep
                {
                    WatchKey = Key.LeftAlt,
                    ThenSteps = [new KeyTapStep { Key = Key.Q }],
                    ElseSteps = [],
                },
                new LoopStep
                {
                    Iterations = 3,
                    Ordering = SequenceType.Burst,
                    BurstCount = 4,
                    Children = [new KeyTapStep { Key = Key.E }],
                },
            ],
        },
    };

    [Fact]
    public void Profile_with_nested_steps_round_trips_through_json()
    {
        var store = new ProfileStore(_directory);
        Profile original = SampleProfile();

        Profile restored = store.Deserialize(store.Serialize(original));

        restored.Should().BeEquivalentTo(original, options => options.RespectingRuntimeTypes());
    }

    [Fact]
    public void Serialized_json_uses_stable_lowercase_type_discriminators()
    {
        var store = new ProfileStore(_directory);

        string json = store.Serialize(SampleProfile());

        json.Should().Contain("\"$type\": \"loop\"");
        json.Should().Contain("\"$type\": \"keyTap\"");
        json.Should().Contain("\"$type\": \"delay\"");
        json.Should().Contain("\"$type\": \"conditionalBranch\"");
    }

    [Fact]
    public void Serialized_hotkey_uses_the_documented_shape()
    {
        var store = new ProfileStore(_directory);

        string json = store.Serialize(SampleProfile());

        json.Should().Contain("\"key\": \"F13\"");
        json.Should().NotContain("modifiers", "the hotkey is modifier-agnostic");
    }

    [Fact]
    public void Save_then_load_round_trips_and_lists_the_profile()
    {
        var store = new ProfileStore(_directory);
        Profile original = SampleProfile();

        store.Save(original);

        store.ListProfiles().Should().Contain("Combat");
        store.Exists("Combat").Should().BeTrue();
        store.Load("Combat").Should().BeEquivalentTo(original, o => o.RespectingRuntimeTypes());
    }

    [Fact]
    public void Delete_removes_the_profile_file()
    {
        var store = new ProfileStore(_directory);
        store.Save(SampleProfile());

        store.Delete("Combat");

        store.Exists("Combat").Should().BeFalse();
        store.ListProfiles().Should().BeEmpty();
    }

    [Fact]
    public void ListProfiles_on_a_missing_directory_returns_empty()
    {
        var store = new ProfileStore(_directory);

        store.ListProfiles().Should().BeEmpty();
    }

    [Fact]
    public void Hand_written_json_in_the_documented_format_deserializes()
    {
        const string json = """
        {
          "name": "Default",
          "hotkey": { "key": "F13", "modifiers": [] },
          "rootStep": {
            "$type": "loop",
            "iterations": null,
            "children": [
              { "$type": "keyTap", "key": "W", "holdMinMs": 30, "holdMaxMs": 50 },
              { "$type": "delay", "minMs": 100, "maxMs": 150 }
            ]
          }
        }
        """;
        var store = new ProfileStore(_directory);

        Profile profile = store.Deserialize(json);

        profile.Name.Should().Be("Default");
        // The legacy "modifiers" array is ignored - hotkey matching is modifier-agnostic.
        profile.Hotkey.Should().Be(new Hotkey(Key.F13));
        profile.RootStep.Should().BeOfType<LoopStep>()
            .Which.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Polymorphic_type_discriminator_is_accepted_out_of_order()
    {
        // A hand-editor may put "$type" after other properties; this must still deserialize.
        const string json = """
        {
          "name": "OutOfOrder",
          "hotkey": { "key": "F14", "modifiers": [] },
          "rootStep": { "minMs": 5, "maxMs": 9, "$type": "delay" }
        }
        """;
        var store = new ProfileStore(_directory);

        Profile profile = store.Deserialize(json);

        profile.RootStep.Should().BeOfType<DelayStep>()
            .Which.MaxMs.Should().Be(9);
    }

    [Fact]
    public void Unbound_hotkey_round_trips()
    {
        var store = new ProfileStore(_directory);
        var original = new Profile { Name = "Empty", Hotkey = Hotkey.None };

        Profile restored = store.Deserialize(store.Serialize(original));

        restored.Hotkey.Should().Be(Hotkey.None);
        restored.Hotkey.IsBound.Should().BeFalse();
    }

    [Fact]
    public void Profile_enabled_flag_round_trips()
    {
        var store = new ProfileStore(_directory);
        var disabled = new Profile { Name = "Parked", Enabled = false };

        store.Deserialize(store.Serialize(disabled)).Enabled.Should().BeFalse();
    }

    [Fact]
    public void Profile_enabled_defaults_to_true_when_absent_from_json()
    {
        var store = new ProfileStore(_directory);

        Profile profile = store.Deserialize("""{ "name": "Legacy" }""");

        profile.Enabled.Should().BeTrue();
    }
}
