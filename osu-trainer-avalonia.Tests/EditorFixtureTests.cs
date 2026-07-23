using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class EditorFixtureTests
    {
        [Fact]
        public void Fixture_LoadsToReady()
        {
            var editor = EditorFixture.LoadReadyEditor();

            Assert.Equal(EditorState.READY, editor.State);
            Assert.NotNull(editor.NewBeatmap);
            Assert.Equal(5M, editor.NewBeatmap.HPDrainRate);
            Assert.Equal(4M, editor.NewBeatmap.CircleSize);
            Assert.Equal(6M, editor.NewBeatmap.OverallDifficulty);
            Assert.Equal(7M, editor.NewBeatmap.ApproachRate);
        }
    }
}
