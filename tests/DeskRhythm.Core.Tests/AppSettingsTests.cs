using DeskRhythm.Core;

namespace DeskRhythm.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_are_as_documented()
    {
        var s = new AppSettings();

        Assert.Equal(30, s.SittingMinutes);
        Assert.Equal(15, s.StandingMinutes);
        Assert.Equal(2, s.MovingMinutes);
        Assert.Equal(MonitorMode.AllMonitors, s.PopupMonitors);
        Assert.Equal(PopupCorner.BottomRight, s.PopupCorner);
        Assert.False(s.SoundEnabled);
        Assert.Equal(5, s.RepromptMinutes);
        Assert.Equal([5, 10, 15], s.SnoozeMinutes);
        Assert.True(s.PauseOnLockOrSleep);
        Assert.False(s.PauseWhenIdle);
        Assert.True(s.HasStandingDesk);
        Assert.Equal(40, s.SitOnlySittingMinutes);
        Assert.Equal(5, s.SitOnlyBreakMinutes);
    }

    [Fact]
    public void MinutesFor_uses_the_routine_of_the_desk_mode()
    {
        var s = new AppSettings { SittingMinutes = 30, StandingMinutes = 15, MovingMinutes = 2, SitOnlySittingMinutes = 40, SitOnlyBreakMinutes = 5 };

        Assert.Equal(30, s.MinutesFor(Phase.Sitting));
        Assert.Equal(2, s.MinutesFor(Phase.Moving));

        s.HasStandingDesk = false;
        Assert.Equal(40, s.MinutesFor(Phase.Sitting));
        Assert.Equal(15, s.MinutesFor(Phase.Standing));
        Assert.Equal(5, s.MinutesFor(Phase.Moving));
    }

    [Fact]
    public void Normalize_clamps_sit_only_values()
    {
        var s = new AppSettings { SitOnlySittingMinutes = 1, SitOnlyBreakMinutes = 99 }.Normalize();

        Assert.Equal(AppSettings.SittingMin, s.SitOnlySittingMinutes);
        Assert.Equal(AppSettings.MovingMax, s.SitOnlyBreakMinutes);
    }

    [Fact]
    public void Old_settings_file_without_desk_fields_keeps_the_standing_desk_routine()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DeskRhythmTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, "{ \"SittingMinutes\": 25, \"StandingMinutes\": 10, \"MovingMinutes\": 3 }");

        try
        {
            var loaded = new JsonFileStore<AppSettings>(path, () => new AppSettings()).Load().Normalize();
            Assert.True(loaded.HasStandingDesk);
            Assert.Equal(25, loaded.SittingMinutes);
            Assert.Equal(40, loaded.SitOnlySittingMinutes);
            Assert.Equal(5, loaded.SitOnlyBreakMinutes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Normalize_clamps_out_of_range_values()
    {
        var s = new AppSettings
        {
            SittingMinutes = 1,
            StandingMinutes = 500,
            MovingMinutes = 0,
            RepromptMinutes = 99,
            SnoozeMinutes = [0, 15, 5, 5, 300, 10, 20],
            PopupMonitors = (MonitorMode)42,
        }.Normalize();

        Assert.Equal(5, s.SittingMinutes);
        Assert.Equal(60, s.StandingMinutes);
        Assert.Equal(1, s.MovingMinutes);
        Assert.Equal(30, s.RepromptMinutes);
        Assert.Equal([5, 10, 15], s.SnoozeMinutes);
        Assert.Equal(MonitorMode.AllMonitors, s.PopupMonitors);
    }

    [Theory]
    [InlineData("5, 10, 15", new[] { 5, 10, 15 })]
    [InlineData("15 5", new[] { 5, 15 })]
    [InlineData("7", new[] { 7 })]
    public void Snooze_text_parses(string text, int[] expected) =>
        Assert.Equal(expected, AppSettings.TryParseSnooze(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0, 5")]
    [InlineData("5, 10, 15, 20")]
    [InlineData("500")]
    public void Bad_snooze_text_is_rejected(string text) =>
        Assert.Null(AppSettings.TryParseSnooze(text));

    [Fact]
    public void Store_round_trips_and_survives_damaged_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DeskRhythmTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        var store = new JsonFileStore<AppSettings>(path, () => new AppSettings());

        try
        {
            store.Save(new AppSettings
            {
                SittingMinutes = 40,
                PopupMonitors = MonitorMode.PrimaryMonitor,
                HasStandingDesk = false,
                SitOnlySittingMinutes = 35,
                SitOnlyBreakMinutes = 4,
            });
            var loaded = store.Load();
            Assert.Equal(40, loaded.SittingMinutes);
            Assert.Equal(MonitorMode.PrimaryMonitor, loaded.PopupMonitors);
            Assert.False(loaded.HasStandingDesk);
            Assert.Equal(35, loaded.SitOnlySittingMinutes);
            Assert.Equal(4, loaded.SitOnlyBreakMinutes);

            File.WriteAllText(path, "{ not json");
            var fallback = store.Load();
            Assert.Equal(30, fallback.SittingMinutes);
            Assert.True(File.Exists(path + ".bad"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Snapshot_round_trips_through_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), "DeskRhythmTests", Guid.NewGuid().ToString("N"));
        var store = new JsonFileStore<RoutineSnapshot>(Path.Combine(dir, "state.json"), () => RoutineSnapshot.Inactive(DateTimeOffset.Now));

        try
        {
            var snapshot = new RoutineSnapshot(true, Phase.Standing, TimeSpan.FromMinutes(7.5), false, DateTimeOffset.Now);
            store.Save(snapshot);
            Assert.Equal(snapshot, store.Load());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
