# osu-trainer-next

Rescale any osu! beatmap — speed, AR, CS, OD, HP — and get a playable copy in a couple of seconds.

A fork of [FunOrange/osu-trainer](https://github.com/FunOrange/osu-trainer) with the UI rebuilt in
Avalonia to match osu!lazer, plus practice tools the original didn't have. The difficulty math and
`.osu` generation are upstream's, unchanged.

> **Status:** in development on the `redesign-avalonia` branch. No release has been published yet —
> build from source for now.

## What it does

Pick a map in osu!. The app follows along and shows it. Move the sliders, hit Generate, and the new
version lands in your library.

**Difficulty**
- Rate from 0.5× to 2.0×, pitch preserved (or not — your call)
- AR, CS, OD, HP, all snapping to clean 0.1 steps
- Star rating recalculates live as you drag
- Scale AR and OD with rate automatically, so a 1.4× map still reads the way you expect
- HR circle-size emulation (CS ×1.3)

**Practice**
- **Practice cut** — trim a diff to a time range and drill just the part you keep failing
- **Rate ladder** — generate a spread of rates in one batch, e.g. `now → +0.10` in 0.05 steps, for
  working your way up to a speed
- **Profiles** — save a set of settings and reapply it to any map in one click

**Getting out of your way**
- Tray icon with a quick-settings flyout — rate and difficulty without opening the main window
- `Ctrl+Alt+Up` / `Ctrl+Alt+Down` nudge the rate from anywhere, including mid-game
- No file paths to type. It reads whatever you have selected in osu!

Options for no-spinner conversions, pitch shifting, and high-quality MP3 encoding live under
**More**.

## How it interacts with osu!

Worth being precise about, since "third-party osu! tool" covers a lot of ground:

- **Reads** osu!'s process memory, read-only, to detect which map you have selected — the same
  approach [StreamCompanion](https://github.com/Piotrekol/StreamCompanion) and
  [gosumemory](https://github.com/l3lackShark/gosumemory) use.
- **Never writes** to the osu! process. No DLL injection, no hooking, no code patching.
- **Never sends input** to the game. Nothing is automated on your behalf.
- **Generates ordinary local maps.** They're unsubmitted, so scores on them aren't ranked and don't
  touch pp — same as upstream osu!trainer has always worked.

It changes what you practice on. It does not change how you play.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and Windows — the tool depends on
reading the osu! process, so it's Windows-only in practice even though Avalonia is cross-platform.

```
git clone --recursive https://github.com/storyut/osu-trainer-next.git
cd osu-trainer-next
git checkout redesign-avalonia
dotnet run --project osu-trainer-avalonia
```

Tests: `dotnet test osu-trainer-avalonia.Tests`

The repo also still contains the original WinForms app under `osu-trainer/`. It builds, but it's
the old UI and isn't where development happens.

## Notes

- Search `osutrainer` in osu! to find everything you've generated.
- Generated maps each carry their own MP3, so a few hundred of them adds up to gigabytes. Delete
  them from within osu! when you're done. (Upstream's "Clean Up" button hasn't been ported to the
  new UI yet.)

## Credits

[FunOrange](https://github.com/FunOrange) for osu-trainer, and
[Craftplacer](https://github.com/Craftplacer) for the original UI work.

## License

[GNU General Public License v3.0](LICENSE), required by its use of
[OsuMemoryDataProvider](https://github.com/Piotrekol/ProcessMemoryDataFinder) (GPL-3.0), which it
links directly.

### Third-party

- [Quicksand](https://fonts.google.com/specimen/Quicksand) — [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL)
- [Lucide](https://lucide.dev/) — [ISC License](https://github.com/lucide-icons/lucide/blob/main/LICENSE), icon vector data only (`Controls/AppIcon`), no NuGet dependency
- [ProcessMemoryDataFinder](https://github.com/Piotrekol/ProcessMemoryDataFinder) — [GPL-3.0](https://github.com/Piotrekol/ProcessMemoryDataFinder/blob/master/LICENSE)
- [FsBeatmapParser](https://github.com/FunOrange/FsBeatmapParser) — vendored under `osu-trainer/submodules/`
- [oppai-ng](https://github.com/Francesco149/oppai-ng) — [Unlicense](https://github.com/Francesco149/oppai-ng/blob/master/UNLICENSE)
- [LAME](https://lame.sourceforge.io/)

Used by the original WinForms UI under `osu-trainer/`:

- [Font Awesome](https://fontawesome.com/) — [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)
- [Comfortaa](https://fonts.google.com/specimen/Comfortaa) — [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL)
- [osu-resources](https://github.com/ppy/osu-resources) — [CC BY 4.0](https://creativecommons.org/licenses/by-nc/4.0/legalcode)
