using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bjs.Cms.Api.Data;
using Bjs.Cms.Contracts;

namespace Bjs.Cms.Api.Services;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static readonly JsonSerializerOptions Pretty = new(Options) { WriteIndented = true };
}

public record SectionEnvelope(string Key, string Version, DateTime? UpdatedAtUtc, string? UpdatedBy, JsonElement Data);

public record SaveResult(bool Ok, bool Conflict, Dictionary<string, string[]>? Errors, SectionEnvelope? Envelope);

public record PublicSnapshot(PublicContent Content, string Json, string ETag);

public sealed class ContentService
{
    readonly IDocumentStore _store;
    readonly IConfiguration _config;
    readonly ILogger<ContentService> _log;
    readonly SemaphoreSlim _writeLock = new(1, 1);
    PublicSnapshot? _snapshot;

    public ContentService(IDocumentStore store, IConfiguration config, ILogger<ContentService> log)
    {
        _store = store;
        _config = config;
        _log = log;
    }

    public static string VersionOf(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..16].ToLowerInvariant();

    // ---------------------------------------------------------------- seeding

    public async Task EnsureSeededAsync()
    {
        var seedDir = Path.Combine(AppContext.BaseDirectory, "Seed");
        foreach (var def in SectionRegistry.All)
        {
            if (await _store.GetAsync(Collections.Content, def.Key) != null) continue;

            object model;
            var file = Path.Combine(seedDir, def.Key + ".json");
            if (File.Exists(file))
            {
                try { model = JsonSerializer.Deserialize(await File.ReadAllTextAsync(file), def.Type, Json.Options) ?? Activator.CreateInstance(def.Type)!; }
                catch (JsonException ex)
                {
                    _log.LogError(ex, "Seed file {File} is invalid; starting {Key} empty.", file, def.Key);
                    model = Activator.CreateInstance(def.Type)!;
                }
            }
            else model = Activator.CreateInstance(def.Type)!;

            var errors = Prepare(def, model);
            if (errors.Count > 0)
                _log.LogWarning("Seed for {Key} has validation problems: {Errors}", def.Key, string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));

            await _store.PutAsync(Collections.Content, def.Key, JsonSerializer.Serialize(model, def.Type, Json.Options), "system");
            _log.LogInformation("Seeded content for '{Key}'.", def.Key);
        }
        _snapshot = null;
    }

    // ---------------------------------------------------------------- reading

    public async Task<IReadOnlyList<(SectionRegistry.Def Def, StoredDoc? Doc)>> ListAsync()
    {
        var docs = (await _store.ListAsync(Collections.Content)).ToDictionary(d => d.Key);
        return SectionRegistry.All.Select(d => (d, docs.GetValueOrDefault(d.Key))).ToList();
    }

    public async Task<SectionEnvelope?> GetAsync(string key)
    {
        var def = SectionRegistry.Find(key);
        if (def == null) return null;
        var doc = await _store.GetAsync(Collections.Content, def.Key);
        if (doc == null)
        {
            var empty = JsonSerializer.Serialize(Activator.CreateInstance(def.Type), def.Type, Json.Options);
            return new SectionEnvelope(def.Key, VersionOf(empty), null, null, JsonDocument.Parse(empty).RootElement.Clone());
        }
        return new SectionEnvelope(def.Key, VersionOf(doc.Json), doc.UpdatedAtUtc, doc.UpdatedBy, JsonDocument.Parse(doc.Json).RootElement.Clone());
    }

    async Task<T> LoadAsync<T>(string key) where T : class, new()
    {
        var doc = await _store.GetAsync(Collections.Content, key);
        if (doc == null) return new T();
        try { return JsonSerializer.Deserialize<T>(doc.Json, Json.Options) ?? new T(); }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Stored content for '{Key}' could not be read; using empty content.", key);
            return new T();
        }
    }

    // ---------------------------------------------------------------- saving

    public async Task<SaveResult> SaveAsync(string key, JsonElement body, string? baseVersion, string user)
    {
        var def = SectionRegistry.Find(key);
        if (def == null) return new(false, false, new() { [""] = ["Unknown section."] }, null);

        object? model;
        try { model = body.Deserialize(def.Type, Json.Options); }
        catch (JsonException ex)
        {
            return new(false, false, new() { [""] = [$"The content is not in the expected format ({ex.Message})."] }, null);
        }
        if (model == null) return new(false, false, new() { [""] = ["No content was sent."] }, null);

        var errors = Prepare(def, model);
        if (errors.Count > 0) return new(false, false, errors, null);

        var json = JsonSerializer.Serialize(model, def.Type, Json.Options);

        await _writeLock.WaitAsync();
        try
        {
            var existing = await _store.GetAsync(Collections.Content, def.Key);
            if (existing != null && baseVersion != null && VersionOf(existing.Json) != baseVersion)
                return new(false, true, null, null);

            await _store.PutAsync(Collections.Content, def.Key, json, user);
            _snapshot = null;
        }
        finally { _writeLock.Release(); }

        return new(true, false, null, await GetAsync(def.Key));
    }

    /// <summary>Clean, tidy and validate a model in place.</summary>
    Dictionary<string, string[]> Prepare(SectionRegistry.Def def, object model)
    {
        var extra = new Dictionary<string, List<string>>();
        Tidy(model, extra);
        var errors = ContentNormalizer.Normalize(model);
        // after normalisation, ids can be generated from cleaned text
        AssignIds(model);
        Semantic(model, extra);
        foreach (var (k, v) in extra)
            errors[k] = errors.TryGetValue(k, out var old) ? old.Concat(v).ToArray() : v.ToArray();
        return errors;
    }

    /// <summary>Rewrites that must happen before validation.</summary>
    static void Tidy(object model, Dictionary<string, List<string>> errors)
    {
        switch (model)
        {
            case ContactSection c:
                c.MapEmbedUrl = ExtractEmbedSrc(c.MapEmbedUrl);
                break;
            case TuitionSection t:
                t.Button.Action = t.Button.Action == "panel" ? "panel" : "link";
                break;
            case GallerySection g:
                foreach (var card in g.Cards)
                    if (!GalleryIcons.Names.Contains(card.Icon)) card.Icon = GalleryIcons.Default;
                break;
        }
    }

    static string ExtractEmbedSrc(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var m = Regex.Match(value, "<iframe[^>]*?\\ssrc\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var src = m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : value;
        return System.Net.WebUtility.HtmlDecode(src).Trim();
    }

    static void AssignIds(object model)
    {
        switch (model)
        {
            case LeadershipSection l:
                Dedupe(l.People.Select(p => (Get: (Func<string>)(() => p.Id), Set: (Action<string>)(v => p.Id = v), p.Name)), "person");
                break;
            case GallerySection g:
                Dedupe(g.Cards.Select(c => (Get: (Func<string>)(() => c.Id), Set: (Action<string>)(v => c.Id = v), Name: c.Title)), "card");
                break;
        }
    }

    static void Dedupe(IEnumerable<(Func<string> Get, Action<string> Set, string Name)> items, string fallback)
    {
        var used = new HashSet<string>();
        foreach (var it in items)
        {
            var id = SiteHelpers.Slug(it.Get());
            if (id.Length == 0) id = SiteHelpers.Slug(it.Name);
            if (id.Length == 0) id = fallback;
            var candidate = id;
            for (int n = 2; !used.Add(candidate); n++) candidate = $"{id}-{n}";
            it.Set(candidate);
        }
    }

    static bool ValidEmail(string s)
    {
        try { var a = new MailAddress(s); return a.Address == s && !s.Contains(' '); }
        catch (FormatException) { return false; }
    }

    static void Semantic(object model, Dictionary<string, List<string>> errors)
    {
        void Add(string path, string msg)
        {
            if (!errors.TryGetValue(path, out var l)) errors[path] = l = new();
            l.Add(msg);
        }

        switch (model)
        {
            case GeneralSettings g:
                if (g.NotificationEmail.Length > 0 && !ValidEmail(g.NotificationEmail)) Add("notificationEmail", "Enter a single, valid email address.");
                if (g.PublicEmail.Length > 0 && !ValidEmail(g.PublicEmail)) Add("publicEmail", "Enter a valid email address.");
                if (g.GtmId.Length > 0 && !Regex.IsMatch(g.GtmId, "^GTM-[A-Z0-9]{4,12}$")) Add("gtmId", "A Tag Manager id looks like GTM-ABC1234.");
                for (int i = 0; i < g.Social.Count; i++)
                    if (!SocialPlatforms.All.Contains(g.Social[i].Platform)) Add($"social[{i}].platform", "Choose one of the supported platforms.");
                break;
            case ContactSection c:
                if (c.Details.Email.Length > 0 && !ValidEmail(c.Details.Email)) Add("details.email", "Enter a valid email address.");
                break;
        }
    }

    // ---------------------------------------------------------------- public payload

    public async Task<PublicSnapshot> GetPublicAsync()
    {
        var cached = _snapshot;
        if (cached != null) return cached;

        var general = await LoadAsync<GeneralSettings>("general");
        var content = new PublicContent
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SiteUrl = (_config["Site:PublicUrl"] ?? "").TrimEnd('/'),
            General = new PublicGeneral
            {
                SiteName = general.SiteName, Tagline = general.Tagline, Logo = general.Logo, LogoDark = general.LogoDark,
                DefaultShareImage = general.DefaultShareImage, Social = general.Social, PublicEmail = general.PublicEmail,
                CookieNoticeEnabled = general.CookieNoticeEnabled, CookieNoticeText = general.CookieNoticeText,
                CookieLinkLabel = general.CookieLinkLabel, GtmId = general.GtmId,
            },
            Menu = await LoadAsync<MenuSection>("menu"),
            Hero = await LoadAsync<HeroSection>("hero"),
            About = await LoadAsync<AboutSection>("about"),
            Leadership = await LoadAsync<LeadershipSection>("leadership"),
            History = await LoadAsync<TextSection>("history"),
            SocialWall = await LoadAsync<GallerySection>("socialwall"),
            Activities = await LoadAsync<GallerySection>("activities"),
            Contact = await LoadAsync<ContactSection>("contact"),
            Tuition = await LoadAsync<TuitionSection>("tuition"),
            Privacy = await LoadAsync<TextSection>("privacy"),
            Footer = await LoadAsync<FooterSection>("footer"),
            BottomStrip = await LoadAsync<BottomStripSection>("bottomstrip"),
        };

        // Version depends on content only (not on GeneratedAtUtc), so it works as a stable ETag.
        content.Version = VersionOf(JsonSerializer.Serialize(with0(content), Json.Options));
        var json = JsonSerializer.Serialize(content, Json.Options);
        return _snapshot = new PublicSnapshot(content, json, "\"" + content.Version + "\"");
    }

    static PublicContent with0(PublicContent c)
    {
        // copy without the volatile timestamp so the version hash is stable between rebuilds of the snapshot
        var copy = JsonSerializer.Deserialize<PublicContent>(JsonSerializer.Serialize(c, Json.Options), Json.Options)!;
        copy.GeneratedAtUtc = default;
        copy.Version = "";
        return copy;
    }

    public void InvalidateSnapshot() => _snapshot = null;
}
