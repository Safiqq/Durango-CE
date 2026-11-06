namespace DurangoLauncher;

public sealed record ReleaseAssets(string ManifestUrl, string FullZipUrl, string LauncherExeUrl = "", string ChunksUrl = "", string BlocksUrl = "");
