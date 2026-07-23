using FsBeatmapProcessor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OsuTrainerCore
{
    // Everything the export names are decided from, as scalars. Deliberately holds no Beatmap:
    // the naming rules are the bug-prone part of export and this is what makes them testable
    // without a map on disk.
    internal readonly record struct ExportNamingInputs
    {
        public string Artist { get; init; }
        public string Title { get; init; }
        public string Creator { get; init; }

        // The map's current names, which the rules append to.
        public string Version { get; init; }
        public string AudioFilename { get; init; }

        // Of the export map, i.e. after the rate has been applied.
        public decimal Bpm { get; init; }

        // Of the original map — taiko and mania have no CS or AR to name.
        public GameMode Mode { get; init; }

        public decimal OriginalHP { get; init; }
        public decimal NewHP { get; init; }
        public decimal OriginalCS { get; init; }
        public decimal NewCS { get; init; }

        // ScaledAR/ScaledOD are the values the rate alone would have produced; AR and OD are
        // named only where the user moved them off that reference (or pushed them past 10).
        public decimal ScaledAR { get; init; }
        public decimal NewAR { get; init; }
        public decimal ScaledOD { get; init; }
        public decimal NewOD { get; init; }

        public decimal Multiplier { get; init; }
        public bool ChangePitch { get; init; }
        public bool PreDT { get; init; }
        public string RangeSuffix { get; init; }
        public bool ForceRateSuffix { get; init; }
    }

    internal readonly record struct ExportNaming(string Version, string AudioFilename, string OsuFileName);

    internal static class ExportNamer
    {
        internal static ExportNaming Describe(ExportNamingInputs inputs)
        {
            string version = inputs.Version;
            string audioFilename = inputs.AudioFilename;
            decimal multiplier = inputs.Multiplier;

            // Difficulty Name and AudioFilename
            if (inputs.PreDT)
            {
                string bpm = inputs.Bpm.ToString("0");
                version += $" {multiplier:0.##}x ({bpm}bpm)";
                audioFilename = $"{Path.GetFileNameWithoutExtension(audioFilename)} {multiplier:0.000}x withDT";
                if (inputs.ChangePitch && Math.Abs(multiplier - 1M) > 0.001M)
                    audioFilename += $" (pitch {(multiplier < 1 ? "lowered" : "raised")})";
                audioFilename += ".mp3";
            }
            // forceRateSuffix (batch generation with more than one rate) always distinguishes the
            // filename by rate, even at exactly 1.00x — otherwise a ladder step at 1.00x would save
            // under the same filename as the unmodified original diff and collide with it in the .osz.
            else if (Math.Abs(multiplier - 1M) > 0.001M || inputs.ForceRateSuffix)
            {
                string bpm = inputs.Bpm.ToString("0");
                version += $" {multiplier:0.##}x ({bpm}bpm)";
                audioFilename = $"{Path.GetFileNameWithoutExtension(audioFilename)} {multiplier:0.000}x";
                if (inputs.ChangePitch)
                    audioFilename += $" (pitch {(multiplier < 1 ? "lowered" : "raised")})";
                audioFilename += ".mp3";
            }

            // Difficulty Name - Difficulty Settings
            string HPCSAROD = "";
            if (inputs.NewHP != inputs.OriginalHP)
                HPCSAROD += $" HP{inputs.NewHP:0.#}";

            if (   inputs.Mode != GameMode.Taiko
                && inputs.Mode != GameMode.Mania
                && inputs.NewCS != inputs.OriginalCS)
                HPCSAROD += $" CS{inputs.NewCS:0.#}";

            if (   inputs.Mode != GameMode.Taiko
                && inputs.Mode != GameMode.Mania
                && (inputs.NewAR != inputs.ScaledAR || inputs.NewAR > 10M))
                HPCSAROD += $" AR{inputs.NewAR:0.#}";

            if (inputs.NewOD != inputs.ScaledOD || inputs.NewOD > 10M)
                HPCSAROD += $" OD{inputs.NewOD:0.#}";

            version += HPCSAROD;

            if (!string.IsNullOrEmpty(inputs.RangeSuffix))
                version += $" {inputs.RangeSuffix}";

            // Beatmap File Name
            string artist  = JunUtils.NormalizeText(inputs.Artist);
            string title   = JunUtils.NormalizeText(inputs.Title);
            string creator = JunUtils.NormalizeText(inputs.Creator);
            string diff    = JunUtils.NormalizeText(version);

            return new ExportNaming(version, audioFilename, $"{artist} - {title} ({creator}) [{diff}].osu");
        }
    }

    // Everything the export steps need from the editor, passed explicitly so the export code
    // reads no editor field of its own.
    internal readonly record struct ExportContext(
        Beatmap OriginalBeatmap,
        Beatmap NewBeatmap,
        decimal BpmRate,
        bool ChangePitch,
        bool NoSpinners,
        bool HighQualityMp3s,
        ICoreHost Host)
    {
        internal decimal ScaledAR => DifficultyCalculator.CalculateMultipliedAR(OriginalBeatmap, BpmRate);

        internal decimal ScaledOD => DifficultyCalculator.CalculateMultipliedOD(OriginalBeatmap, BpmRate);
    }

    internal sealed record ExportArtifact(string OsuPath, string Mp3Path);

    internal static class BeatmapExporter
    {
        internal static (ExportArtifact Artifact, int SurvivingObjects) BuildArtifact(ExportContext ctx, BeatmapEditor.PracticeRange? range, string rangeSuffix, bool forceRateSuffix = false)
        {
            bool compensateForDT = (ctx.NewBeatmap.ApproachRate > 10 || ctx.NewBeatmap.OverallDifficulty > 10);

            // Set metadata
            Beatmap exportBeatmap = new Beatmap(ctx.NewBeatmap);
            ApplyNaming(exportBeatmap, ctx, ctx.BpmRate, ctx.ChangePitch, compensateForDT, range != null ? rangeSuffix : null, forceRateSuffix);

            // Slow down map by 1.5x
            if (compensateForDT)
            {
                exportBeatmap.ApproachRate      = DifficultyCalculator.CalculateMultipliedAR(ctx.NewBeatmap, 1 / 1.5M);
                exportBeatmap.OverallDifficulty = DifficultyCalculator.CalculateMultipliedOD(ctx.NewBeatmap, 1 / 1.5M);
                decimal compensatedRate = (ctx.NewBeatmap.Bpm / ctx.OriginalBeatmap.Bpm) / 1.5M;
                exportBeatmap.SetRate(compensatedRate);
            }

            // remove spinners
            if (ctx.NoSpinners)
                exportBeatmap.RemoveSpinners();

            // Generate new mp3
            var audioFilePath = Path.Combine(JunUtils.GetBeatmapDirectoryName(ctx.OriginalBeatmap), exportBeatmap.AudioFilename);
            string newMp3 = null;
            if (!File.Exists(audioFilePath))
            {
                string inFile = Path.Combine(Path.GetDirectoryName(ctx.OriginalBeatmap.Filename), ctx.OriginalBeatmap.AudioFilename);
                string outFile = exportBeatmap.AudioFilename;

                SongSpeedChanger.GenerateAudioFile(inFile, outFile, ctx.BpmRate, ctx.ChangePitch, compensateForDT, ctx.HighQualityMp3s);
                newMp3 = outFile;

                // take note of this mp3 in a text file so we can clean it up later
                string mp3ManifestFile = GetMp3ListFilePath();
                List<string> manifest = File.ReadAllLines(mp3ManifestFile).ToList();
                string beatmapFolder = Path.GetDirectoryName(exportBeatmap.Filename).Replace(JunUtils.SongsFolder + "\\", "");
                string mp3RelativePath = Path.Combine(beatmapFolder, exportBeatmap.AudioFilename);
                manifest.Add(mp3RelativePath + " | " + exportBeatmap.Filename);
                File.WriteAllLines(mp3ManifestFile, manifest);
            }
            // save .osu to temp location (do not directly put into any song folder)
            exportBeatmap.Filename = Path.GetFileName(exportBeatmap.Filename);
            exportBeatmap.Save();

            int survivingObjects = 0;
            if (range != null)
                survivingObjects = PracticeCut.TrimOsuFile(exportBeatmap.Filename, range.Value.StartMs, range.Value.EndMs);

            return (new ExportArtifact(exportBeatmap.Filename, newMp3), survivingObjects);
        }

        internal static void PublishOsz(ExportContext ctx, string songFolder, IReadOnlyList<ExportArtifact> artifacts)
        {
            // 1. Create osz (just a regular zip file with file ext. renamed to .osz)
            string outputOsz = Path.GetFileNameWithoutExtension(songFolder) + ".osz";
            if (File.Exists(outputOsz))
                File.Delete(outputOsz);
            try
            {
                // NoCompression: song folders hold mp3/jpg/mp4, already compressed. Deflating them
                // was measured at 26-47x slower for ~7% smaller output on real libraries (unit 016).
                ZipFile.CreateFromDirectory(songFolder, outputOsz, CompressionLevel.NoCompression, includeBaseDirectory: false);
            }
            catch (Exception e)
            {
                ctx.Host.ShowError($"Failed to create {outputOsz} {Environment.NewLine}{Environment.NewLine}{e.Message}");
            }
            // 2. Add new files to zip/osz
            using (ZipArchive archive = ZipFile.Open(outputOsz, ZipArchiveMode.Update))
            {
                foreach (var artifact in artifacts)
                {
                    archive.CreateEntryFromFile(artifact.OsuPath, Path.GetFileName(artifact.OsuPath), CompressionLevel.NoCompression);
                    if (artifact.Mp3Path != null)
                        archive.CreateEntryFromFile(artifact.Mp3Path, Path.GetFileName(artifact.Mp3Path), CompressionLevel.NoCompression);
                }
            }
            // 3. Run the .osz
            Process proc = new Process();
            proc.StartInfo.FileName = outputOsz;
            proc.StartInfo.UseShellExecute = true;
            try
            {
                proc.Start();
            }
            catch
            {
                ctx.Host.ShowError("There was an error opening the generated .osz file. This is probably because .osz files have not been configured to open with osu!.exe on this system." + Environment.NewLine + Environment.NewLine +
                    "To fix this, download any map from the website, right click the .osz file, click properties, beside Opens with... click Change..., and select osu!. " +
                    "You'll know the problem is fixed when you can double click .osz files to open them with osu!");
            }
        }

        // Best-effort temp cleanup: a file may already be gone (e.g. GenerateBeatmap's own
        // .osu path after a successful PublishOsz), which is not a failure worth surfacing.
        // Any other IOException is logged so a stray temp file is still diagnosable (OPS-1).
        internal static void DeleteArtifacts(IReadOnlyList<ExportArtifact> artifacts)
        {
            foreach (var artifact in artifacts)
            {
                DeleteIfExists(artifact.OsuPath);
                if (artifact.Mp3Path != null)
                    DeleteIfExists(artifact.Mp3Path);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (!File.Exists(path))
                return;
            try
            {
                File.Delete(path);
            }
            catch (IOException e)
            {
                Console.WriteLine($"Failed to delete temp artifact {path}: {e.Message}");
            }
        }

        internal static string GetMp3ListFilePath()
        {
            string manifest = Path.Combine(JunUtils.SongsFolder, "modified_mp3_list.txt");
            if (!File.Exists(manifest))
            {
                FileStream fs = File.Open(manifest, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                fs.Close();
                fs.Dispose();
            }
            return manifest;
        }

        // OUT: map.Version
        // OUT: map.Filename
        // OUT: map.AudioFilename
        // OUT: map.Tags
        internal static void ApplyNaming(Beatmap map, ExportContext ctx, decimal multiplier, bool changePitch = false, bool preDT = false, string rangeSuffix = null, bool forceRateSuffix = false)
        {
            var naming = ExportNamer.Describe(new ExportNamingInputs
            {
                Artist          = map.Artist,
                Title           = map.Title,
                Creator         = map.Creator,
                Version         = map.Version,
                AudioFilename   = map.AudioFilename,
                Bpm             = map.Bpm,
                Mode            = ctx.OriginalBeatmap.Mode,
                OriginalHP      = ctx.OriginalBeatmap.HPDrainRate,
                NewHP           = ctx.NewBeatmap.HPDrainRate,
                OriginalCS      = ctx.OriginalBeatmap.CircleSize,
                NewCS           = ctx.NewBeatmap.CircleSize,
                ScaledAR        = ctx.ScaledAR,
                NewAR           = ctx.NewBeatmap.ApproachRate,
                ScaledOD        = ctx.ScaledOD,
                NewOD           = ctx.NewBeatmap.OverallDifficulty,
                Multiplier      = multiplier,
                ChangePitch     = changePitch,
                PreDT           = preDT,
                RangeSuffix     = rangeSuffix,
                ForceRateSuffix = forceRateSuffix,
            });

            map.Version = naming.Version;
            // Guarded, not unconditional: the old code only touched AudioFilename inside its two
            // rate branches, and a map with no AudioFilename line at all would otherwise gain an
            // empty one on save. Same reason the concatenation below is not Path.Combine.
            if (naming.AudioFilename != map.AudioFilename)
                map.AudioFilename = naming.AudioFilename;
            map.Filename = Path.GetDirectoryName(map.Filename) + $"\\{naming.OsuFileName}";

            // make this map searchable in the in-game menus
            var TagsWithOsutrainer = map.Tags;
            TagsWithOsutrainer.Add("osutrainer");
            map.Tags = TagsWithOsutrainer; // need to assign like this because Tags is an immutable list
        }
    }
}
