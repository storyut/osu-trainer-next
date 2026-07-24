# osu-trainer-next

A fork of [FunOrange/osu-trainer](https://github.com/FunOrange/osu-trainer) with the UI rebuilt to
look like it belongs to osu!, plus a few practice tools the original doesn't have.

The tool itself works the way it always has. This page only covers what's different.

> **Status:** in development on the `redesign-avalonia` branch. No release published yet — build
> from source.

## The redesign

The original is WinForms, and looks it. This is a ground-up rebuild in
[Avalonia](https://avaloniaui.net/) following osu!lazer's design language.

## What's new

**Practice cut** — trim a diff to a time range and drill only the part you keep failing, instead of
replaying two minutes of intro to reach it.

**Rate ladder** — generate a spread of rates as one batch rather than one map at a time. Presets are
anchored to wherever the slider currently sits (`now → +0.10`, `±0.10 around`, and so on) at a 0.05
or 0.10 step, so working up to a speed is one click instead of eight.

**Tray quick-settings** — a flyout from the tray icon with the rate and difficulty rows, sharing
state with the main window. Adjust and generate without bringing the full window up.

**Settings that survive a restart** — rate, locks, and every toggle persist to disk. Upstream keeps
these in memory only, so they reset every launch.

Runs on .NET 8 rather than .NET Framework 4.7.1.

## Not ported yet

- **Edit Hotkeys** — the rate-nudge hotkeys work (`Ctrl+Alt+Up` / `Ctrl+Alt+Down`), but they aren't
  rebindable in this UI yet. Upstream's hotkey editor has no equivalent here.
- **Clean Up** — upstream's button for deleting generated MP3s. Delete generated maps from within
  osu! for now.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) and Windows.

```
git clone --recursive https://github.com/storyut/osu-trainer-next.git
cd osu-trainer-next
git checkout redesign-avalonia
dotnet run --project osu-trainer-avalonia
```

Tests: `dotnet test osu-trainer-avalonia.Tests`

The original WinForms app is still in the tree under `osu-trainer/`. It builds, but development
happens in `osu-trainer-avalonia/`.

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
