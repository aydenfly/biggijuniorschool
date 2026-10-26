using System.Text.Json;
using Bjs.Cms.Api.Data;

namespace Bjs.Cms.Api.Services;

public class MediaAsset
{
    public string Id { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string Ext { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Alt { get; set; } = "";
    public int[] Variants { get; set; } = [];
    /// <summary>Main file: /uploads/{id}-4k.{ext}. Smaller versions follow the same pattern (-480, -800...).</summary>
    public string Url { get; set; } = "";
    public string UploadedBy { get; set; } = "";
    public DateTime UploadedAtUtc { get; set; }
}

public sealed class MediaService
{
    public static readonly int[] AllowedVariants = [320, 480, 640, 800];
    public const long MaxMainBytes = 12 * 1024 * 1024;
    public const long MaxVariantBytes = 4 * 1024 * 1024;

    readonly IDocumentStore _store;
    public string Root { get; }

    public MediaService(IDocumentStore store, IConfiguration config, IHostEnvironment env)
    {
        _store = store;
        var configured = config["Storage:UploadsPath"];
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? Path.Combine(env.ContentRootPath, "App_Data", "uploads") : configured);
        Directory.CreateDirectory(Root);
    }

    /// <summary>Identifies an image by its first bytes. Returns the extension, or null if it is not an allowed image.</summary>
    public static (string Ext, string ContentType)? Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("jpg", "image/jpeg");
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ("png", "image/png");
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return ("gif", "image/gif");
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return ("webp", "image/webp");
        return null;
    }

    static MediaAsset? Read(StoredDoc d) => JsonSerializer.Deserialize<MediaAsset>(d.Json, Json.Options);

    public async Task<IReadOnlyList<MediaAsset>> ListAsync() =>
        (await _store.ListAsync(Collections.Media)).Select(Read).Where(m => m != null).Select(m => m!).OrderByDescending(m => m.UploadedAtUtc).ToList();

    public async Task<MediaAsset?> GetAsync(string id)
    {
        var d = await _store.GetAsync(Collections.Media, id);
        return d == null ? null : Read(d);
    }

    public async Task<(MediaAsset? Asset, string? Error)> SaveAsync(
        string originalName, byte[] main, Dictionary<int, byte[]> variants, int width, int height, string alt, string user)
    {
        if (main.Length == 0) return (null, "The file is empty.");
        if (main.Length > MaxMainBytes) return (null, "That image is larger than 12 MB.");
        var kind = Sniff(main);
        if (kind == null) return (null, "Only JPEG, PNG, GIF and WebP images can be uploaded.");

        foreach (var (w, bytes) in variants)
        {
            if (!AllowedVariants.Contains(w)) return (null, "Unexpected image size.");
            if (bytes.Length > MaxVariantBytes) return (null, "A resized copy is too large.");
            if (Sniff(bytes)?.Ext != kind.Value.Ext) return (null, "Resized copies must be the same type as the original.");
        }

        var id = Guid.NewGuid().ToString("N")[..12];
        var ext = kind.Value.Ext;
        await File.WriteAllBytesAsync(Path.Combine(Root, $"{id}-4k.{ext}"), main);
        foreach (var (w, bytes) in variants)
            await File.WriteAllBytesAsync(Path.Combine(Root, $"{id}-{w}.{ext}"), bytes);

        var asset = new MediaAsset
        {
            Id = id, OriginalName = SafeName(originalName), Ext = ext, ContentType = kind.Value.ContentType, Size = main.Length,
            Width = Math.Clamp(width, 0, 20000), Height = Math.Clamp(height, 0, 20000),
            Alt = alt.Length > 200 ? alt[..200] : alt.Trim(),
            Variants = variants.Keys.OrderBy(k => k).ToArray(), Url = $"/uploads/{id}-4k.{ext}",
            UploadedBy = user, UploadedAtUtc = DateTime.UtcNow,
        };
        await _store.PutAsync(Collections.Media, id, JsonSerializer.Serialize(asset, Json.Options), user);
        return (asset, null);
    }

    static string SafeName(string n)
    {
        n = Path.GetFileName(n ?? "");
        var clean = new string(n.Where(c => !char.IsControl(c)).ToArray());
        return clean.Length > 150 ? clean[..150] : clean;
    }

    public async Task<MediaAsset?> UpdateAltAsync(string id, string alt, string user)
    {
        var m = await GetAsync(id);
        if (m == null) return null;
        m.Alt = alt.Trim().Length > 200 ? alt.Trim()[..200] : alt.Trim();
        await _store.PutAsync(Collections.Media, id, JsonSerializer.Serialize(m, Json.Options), user);
        return m;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var m = await GetAsync(id);
        if (m == null) return false;
        foreach (var f in Directory.EnumerateFiles(Root, $"{m.Id}-*.{m.Ext}")) File.Delete(f);
        return await _store.DeleteAsync(Collections.Media, id);
    }
}
