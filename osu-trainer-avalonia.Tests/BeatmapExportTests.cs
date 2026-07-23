using System;
using System.IO;
using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    // JunUtils.SongsFolder is process-global mutable state (and must be written fully qualified:
    // the F# submodule exports a JunUtils of its own). Any test that writes it must join this
    // collection so xunit will not run it beside another test that also writes it.
    [CollectionDefinition("SongsFolder")]
    public sealed class SongsFolderCollection { }

    [Collection("SongsFolder")]
    public class BeatmapExportTests
    {
        // The one branch of BuildArtifact that can run hermetically: the fixture's audio.mp3
        // already sits beside minimal.osu, so the export reuses it instead of shelling out to
        // soundstretch.exe — which also means no manifest line is appended.
        [Fact]
        public void BuildArtifactReusesExistingAudioAndWritesOneOsu()
        {
            string previousSongsFolder = OsuTrainerCore.JunUtils.SongsFolder;
            string songsFolder = Path.Combine(Path.GetTempPath(), "osu-trainer-next-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(songsFolder);
            string? writtenOsu = null;

            try
            {
                OsuTrainerCore.JunUtils.SongsFolder = songsFolder;

                var editor = EditorFixture.LoadReadyEditor();
                var ctx = new ExportContext(editor.OriginalBeatmap, editor.NewBeatmap, 1.0M, false, false, false, null!);

                var (artifact, survivingObjects) = BeatmapExporter.BuildArtifact(ctx, null, null);
                writtenOsu = artifact.OsuPath;

                Assert.Null(artifact.Mp3Path);
                Assert.Equal(0, survivingObjects); // no practice range was requested
                Assert.True(File.Exists(artifact.OsuPath));
                Assert.Equal("Fixture Artist - Fixture Title (fablely) [Normal].osu", Path.GetFileName(artifact.OsuPath));
                Assert.False(File.Exists(Path.Combine(songsFolder, "modified_mp3_list.txt")));
            }
            finally
            {
                OsuTrainerCore.JunUtils.SongsFolder = previousSongsFolder;
                if (writtenOsu != null && File.Exists(writtenOsu))
                    File.Delete(writtenOsu);
                Directory.Delete(songsFolder, recursive: true);
            }
        }
    }
}
