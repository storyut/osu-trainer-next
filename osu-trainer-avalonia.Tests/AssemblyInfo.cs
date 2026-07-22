using System.Runtime.Versioning;

// osu-trainer-avalonia (referenced here) is Windows-only (custom chrome + Win32
// global-hotkey P/Invoke); this project only exercises its types, so it inherits the same
// declaration to avoid CA1416 warnings at every call site.
[assembly: SupportedOSPlatform("windows")]
