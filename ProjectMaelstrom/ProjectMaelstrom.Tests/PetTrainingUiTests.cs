using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class PetTrainingUiTests
{
    [Fact]
    public void SavedPreferencesAppearBeforeTheUserCanSaveAgain()
    {
        OnSta(() =>
        {
            using var form = new PetTrainingPreferencesForm(new(Snacks: PetSnackPreference.SuggestOnly,
                Goal: PetTrainingGoal.Agility, GoalTarget: 150));
            var combos = Descendants(form).OfType<ComboBox>().ToArray();
            Assert.Equal(PetSnackPreference.SuggestOnly, combos.Single(combo => combo.AccessibleName == "Snack policy").SelectedItem);
            Assert.Equal(PetTrainingGoal.Agility, combos.Single(combo => combo.AccessibleName == "Pet training goal").SelectedItem);
            Assert.False(form.Visible);
        });
    }

    private static IEnumerable<Control> Descendants(Control parent)
        => parent.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));

    [Fact]
    public void PausedVideoPreviewStillPreventsSwitchingGames()
    {
        OnSta(() =>
        {
            using var form = new GameWorkspaceForm(dataRoot: Path.GetTempPath());
            var name = Descendants(form).OfType<TextBox>().Single(box => box.AccessibleName == "Workspace name");
            name.Text = "Original recording";
            var field = typeof(GameWorkspaceForm).GetField("_videoTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var recording = new TaskCompletionSource();
            field.SetValue(form, recording.Task);
            try
            {
                form.PreferDodge();
                Assert.Equal("Original recording", name.Text);
                form.PreferWizard101();
                Assert.Equal("Original recording", name.Text);
                Assert.False(form.Visible);
            }
            finally { recording.SetResult(); field.SetValue(form, null); }
        });
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Owned form initialization timed out.");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
