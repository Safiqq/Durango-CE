namespace DurangoLauncher;

public sealed record CalFetchResult(CalFetchKind Kind, CalendarData? Data, string Detail = "");
