using System.Runtime.Versioning;

// Custom-chrome window + Win32 global-hotkey P/Invoke (Interop/GlobalHotKey.cs) make this
// app Windows-only already; declaring it assembly-wide silences CA1416 instead of
// annotating every call site individually.
[assembly: SupportedOSPlatform("windows")]
