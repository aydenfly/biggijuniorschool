namespace BJS.Master.Services;

/// <summary>SVG path data for the icons editors can choose for gallery cards (24x24 viewBox).</summary>
public static class GalleryIconPaths
{
    static readonly Dictionary<string, string> Paths = new()
    {
        ["ball"] = "M12 2a10 10 0 1 0 .001 20.001A10 10 0 0 0 12 2Zm0 2.6 2.6 1.9-1 3.1h-3.2l-1-3.1L12 4.6ZM5.3 8.9l2.7-.2.9 2.9-2.2 2-2.5-1.2-.4-2.8 1.5-.7Zm13.4 0 1.5.7-.4 2.8-2.5 1.2-2.2-2 .9-2.9 2.7.2ZM9.4 13.6h5.2l1.3 3-2.9 2.6h-2l-2.9-2.6 1.3-3Z",
        ["book"] = "M4 4.5A1.5 1.5 0 0 1 5.5 3H11v18H5.5A1.5 1.5 0 0 1 4 19.5v-15ZM13 3h5.5A1.5 1.5 0 0 1 20 4.5v15a1.5 1.5 0 0 1-1.5 1.5H13V3Z",
        ["calendar"] = "M7 2h2v2h6V2h2v2h3a1 1 0 0 1 1 1v15a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h3V2Zm13 8H4v10h16V10ZM4 7v1h16V7H4Z",
        ["camera"] = "M9 3 7.2 5H4a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-3.2L15 3H9Zm3 5a4.5 4.5 0 1 1 0 9 4.5 4.5 0 0 1 0-9Zm0 2a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5Z",
        ["cap"] = "M12 3 1 8l11 5 9-4.1V16h2V8L12 3Zm-7 9.5V16c0 2 3.1 4 7 4s7-2 7-4v-3.5l-7 3.2-7-3.2Z",
        ["chat"] = "M4 4h16a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H9l-4.5 4V16H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z",
        ["dining"] = "M6 2v8.5a2.5 2.5 0 0 0 2 2.45V22h2v-9.05A2.5 2.5 0 0 0 12 10.5V2h-1.5v6H9.8V2H8.3v6H7V2H6Zm11 0c-2 0-3.5 2.2-3.5 6s1.2 5.6 2.5 6V22h2V4c0-1.1-.4-2-1-2Z",
        ["flask"] = "M9 2h6v2h-1v5.3l4.4 7.6a2 2 0 0 1-1.7 3H7.3a2 2 0 0 1-1.7-3L10 9.3V4H9V2Zm2 2v5.7l-1 1.8h4l-1-1.8V4h-2Z",
        ["home"] = "M12 3 2 11h3v9h5v-6h4v6h5v-9h3L12 3Z",
        ["music"] = "M9 17a3 3 0 1 1-2-2.83V4.5a.5.5 0 0 1 .62-.49l9 2.25a.5.5 0 0 1 .38.49V15a3 3 0 1 1-2-2.83V7.14L9 5.4v8.77c.32.22.6.5.82.83H9Z",
        ["netball"] = "M12 2a7 7 0 1 0 .001 14.001A7 7 0 0 0 12 2Zm0 2.4A4.6 4.6 0 1 1 7.4 9 4.6 4.6 0 0 1 12 4.4ZM11 16v6h2v-6h-2Zm-4 6 1.6-4h2.1l-1.9 4H7Zm7.7 0-1.9-4h2.1l1.6 4h-1.8Z",
        ["run"] = "M14.1 3a1.7 1.7 0 1 1 0 3.4 1.7 1.7 0 0 1 0-3.4Zm-2.5 5 3 1.4 2 3.6-1.5.9-1.6-2.9-1.3-.6-.9 3.3 2.8 2.6-.5 4.7-1.6-.1.4-3.7-2.8-2.6.3-4.6-2.9 1.3-.8 2.8-1.5-.5 1.1-3.7L11 8l.6.1Z",
    };

    public static string For(string? name) =>
        name != null && Paths.TryGetValue(name, out var d) ? d : Paths["home"];
}
