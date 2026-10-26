using System.Security.Claims;
using System.Text.Json;
using Bjs.Cms.Api.Services;
using Bjs.Cms.Contracts;

namespace Bjs.Cms.Api.Endpoints;

public static class AdminEndpoints
{
    public record AltRequest(string? Alt);
    public record ReadRequest(bool Read);
    public record NewUserRequest(string? Email, string? Name, string? Role, string? Password);
    public record UpdateUserRequest(string? Name, string? Role);
    public record ResetPasswordRequest(string? NewPassword);

    static string Who(ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.Name) ?? p.FindFirstValue(ClaimTypes.Email) ?? "unknown";

    static IResult Bad(string message, object? errors = null) => Results.Json(new { message, errors }, statusCode: 400);

    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization();

        // ------------------------------------------------------------ meta + dashboard

        g.MapGet("/meta", (IConfiguration config, MailService mail) => Results.Ok(new
        {
            siteUrl = (config["Site:PublicUrl"] ?? "").TrimEnd('/'),
            routes = SiteRoutes.All,
            icons = GalleryIcons.Names,
            platforms = SocialPlatforms.All,
            mail = new { provider = mail.Provider, configured = mail.IsConfigured },
            upload = new { maxMegabytes = MediaService.MaxMainBytes / 1024 / 1024, variants = MediaService.AllowedVariants },
        }));

        g.MapGet("/sections", async (ClaimsPrincipal p, ContentService content) =>
        {
            var isAdmin = p.IsInRole(Roles.Admin);
            var rows = await content.ListAsync();
            return Results.Ok(rows.Where(r => isAdmin || r.Def.Key != "general").Select(r =>
            {
                var seoDone = r.Def.HasSeo ? SeoComplete(r.Doc?.Json) : (bool?)null;
                return new
                {
                    key = r.Def.Key, title = r.Def.Title, group = r.Def.Group, description = r.Def.Description,
                    route = r.Def.Route, hasSeo = r.Def.HasSeo, seoComplete = seoDone,
                    updatedAtUtc = r.Doc?.UpdatedAtUtc, updatedBy = r.Doc?.UpdatedBy,
                };
            }));
        });

        g.MapGet("/dashboard", async (ClaimsPrincipal p, ContentService content, MediaService media, EnquiryService enquiries,
                                      UserService users, MailService mail) =>
        {
            var rows = await content.ListAsync();
            var mediaList = await media.ListAsync();
            var enquiryList = await enquiries.ListAsync();
            var userCount = (await users.ListAsync()).Count;
            var me = await users.GetAsync(p.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

            var seoRows = rows.Where(r => r.Def.HasSeo).ToList();
            var seoDone = seoRows.Count(r => SeoComplete(r.Doc?.Json));

            T? Load<T>(string key) where T : class
            {
                var doc = rows.FirstOrDefault(r => r.Def.Key == key).Doc;
                return doc == null ? null : JsonSerializer.Deserialize<T>(doc.Json, Json.Options);
            }

            var general = Load<GeneralSettings>("general") ?? new();
            var hero = Load<HeroSection>("hero") ?? new();
            var contact = Load<ContactSection>("contact") ?? new();

            var checklist = new List<object>
            {
                new { id = "password", label = "Replace your temporary password", ok = me?.MustChangePassword != true, link = "/account", detail = "You are still using the password that was generated on first start." },
                new { id = "notify", label = "Choose where contact form messages are sent", ok = general.NotificationEmail.Length > 0, link = "/settings/general", detail = "Messages are kept in the Enquiries inbox either way." },
                new { id = "mail", label = "Email delivery is set up on the server", ok = mail.IsConfigured, link = (string?)null, detail = "Set Mail:Provider in appsettings.json (Mailjet or Smtp). Until then enquiries are saved but not emailed." },
                new { id = "seo", label = "Every page has a meta title and description", ok = seoDone == seoRows.Count, link = "/seo", detail = $"{seoRows.Count - seoDone} of {seoRows.Count} pages need attention." },
                new { id = "hero", label = "The homepage slideshow has at least one image", ok = hero.Slides.Count > 0, link = "/content/hero", detail = "" },
                new { id = "map", label = "The contact map is set", ok = contact.MapEmbedUrl.Length > 0, link = "/content/contact", detail = "" },
                new { id = "share", label = "A default share image is set", ok = general.DefaultShareImage.Length > 0, link = "/settings/general", detail = "Shown when a page is shared on WhatsApp or Facebook." },
            };

            return Results.Ok(new
            {
                stats = new
                {
                    sections = rows.Count, seoComplete = seoDone, seoTotal = seoRows.Count, media = mediaList.Count,
                    enquiriesUnread = enquiryList.Count(e => !e.Read), enquiriesTotal = enquiryList.Count, users = userCount,
                },
                recentEdits = rows.Where(r => r.Doc != null && r.Doc.UpdatedBy != "system")
                    .OrderByDescending(r => r.Doc!.UpdatedAtUtc).Take(6)
                    .Select(r => new { key = r.Def.Key, title = r.Def.Title, updatedAtUtc = r.Doc!.UpdatedAtUtc, updatedBy = r.Doc.UpdatedBy }),
                recentEnquiries = enquiryList.Take(5),
                checklist,
            });
        });

        // ------------------------------------------------------------ content

        g.MapGet("/content/{key}", async (string key, ClaimsPrincipal p, ContentService content) =>
        {
            if (key.Equals("general", StringComparison.OrdinalIgnoreCase) && !p.IsInRole(Roles.Admin)) return Results.Forbid();
            var env = await content.GetAsync(key);
            return env == null ? Results.NotFound(new { message = "Unknown section." }) : Results.Ok(env);
        });

        g.MapPut("/content/{key}", async (string key, HttpContext ctx, JsonElement body, ClaimsPrincipal p, ContentService content) =>
        {
            if (key.Equals("general", StringComparison.OrdinalIgnoreCase) && !p.IsInRole(Roles.Admin)) return Results.Forbid();
            if (SectionRegistry.Find(key) == null) return Results.NotFound(new { message = "Unknown section." });

            var ifMatch = ctx.Request.Headers.IfMatch.ToString().Trim().Trim('"');
            var result = await content.SaveAsync(key, body, ifMatch.Length > 0 ? ifMatch : null, Who(p));

            if (result.Conflict)
                return Results.Json(new { message = "Someone else saved changes to this section while you were editing. Reload to see their version." }, statusCode: 409);
            if (!result.Ok)
                return Bad("Some fields need attention before this can be saved.", result.Errors);
            return Results.Ok(result.Envelope);
        });

        // ------------------------------------------------------------ media

        g.MapGet("/media", async (MediaService media, ContentService content) =>
        {
            var items = await media.ListAsync();
            var rows = await content.ListAsync();
            return Results.Ok(items.Select(m => new
            {
                m.Id, m.OriginalName, m.Ext, m.ContentType, m.Size, m.Width, m.Height, m.Alt, m.Variants, m.Url, m.UploadedBy, m.UploadedAtUtc,
                usedIn = rows.Where(r => r.Doc != null && r.Doc.Json.Contains($"/uploads/{m.Id}-")).Select(r => r.Def.Title).ToArray(),
            }));
        });

        g.MapPost("/media", async (HttpRequest req, ClaimsPrincipal p, MediaService media) =>
        {
            if (!req.HasFormContentType) return Bad("Send the image as a form upload.");
            var form = await req.ReadFormAsync();
            var file = form.Files["file"];
            if (file == null) return Bad("Choose an image to upload.");

            static async Task<byte[]> Bytes(IFormFile f) { using var ms = new MemoryStream(); await f.CopyToAsync(ms); return ms.ToArray(); }

            var variants = new Dictionary<int, byte[]>();
            foreach (var w in MediaService.AllowedVariants)
                if (form.Files["v" + w] is { Length: > 0 } vf) variants[w] = await Bytes(vf);

            int.TryParse(form["width"], out var width);
            int.TryParse(form["height"], out var height);

            var (asset, error) = await media.SaveAsync(file.FileName, await Bytes(file), variants, width, height, form["alt"].ToString(), Who(p));
            return asset == null ? Bad(error ?? "Upload failed.") : Results.Ok(asset);
        });

        g.MapPatch("/media/{id}", async (string id, AltRequest body, ClaimsPrincipal p, MediaService media) =>
        {
            var m = await media.UpdateAltAsync(id, body.Alt ?? "", Who(p));
            return m == null ? Results.NotFound() : Results.Ok(m);
        });

        g.MapDelete("/media/{id}", async (string id, MediaService media) =>
            await media.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        // ------------------------------------------------------------ enquiries

        g.MapGet("/enquiries", async (EnquiryService enquiries) => Results.Ok(await enquiries.ListAsync()));

        g.MapPatch("/enquiries/{id}", async (string id, ReadRequest body, EnquiryService enquiries) =>
        {
            var e = await enquiries.SetReadAsync(id, body.Read);
            return e == null ? Results.NotFound() : Results.Ok(e);
        });

        g.MapDelete("/enquiries/{id}", async (string id, EnquiryService enquiries) =>
            await enquiries.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());

        // ------------------------------------------------------------ users (administrators only)

        var u = g.MapGroup("/users").RequireAuthorization(policy => policy.RequireRole(Roles.Admin));

        u.MapGet("/", async (UserService users) =>
            Results.Ok((await users.ListAsync()).Select(x => new { x.Id, x.Email, x.Name, x.Role, x.MustChangePassword, x.CreatedAtUtc, x.LastLoginUtc })));

        u.MapPost("/", async (NewUserRequest body, UserService users) =>
        {
            var (user, error) = await users.CreateAsync(body.Email ?? "", body.Name ?? "", body.Role ?? "", body.Password ?? "");
            return user == null ? Bad(error ?? "Could not add the user.") : Results.Ok(AuthEndpoints.Me(user));
        });

        u.MapPatch("/{id}", async (string id, UpdateUserRequest body, UserService users) =>
        {
            var user = await users.GetAsync(id);
            if (user == null) return Results.NotFound();
            var (ok, error) = await users.UpdateAsync(user, body.Name, body.Role);
            return ok ? Results.Ok(AuthEndpoints.Me(user)) : Bad(error ?? "Could not update the user.");
        });

        u.MapPost("/{id}/reset-password", async (string id, ResetPasswordRequest body, UserService users) =>
        {
            var user = await users.GetAsync(id);
            if (user == null) return Results.NotFound();
            if (string.IsNullOrEmpty(body.NewPassword)) return Bad("Enter a new password.");
            if (UserService.PasswordProblem(body.NewPassword) is { } problem) return Bad(problem);
            await users.SetPasswordAsync(user, body.NewPassword, mustChange: true);
            return Results.NoContent();
        });

        u.MapDelete("/{id}", async (string id, ClaimsPrincipal p, UserService users) =>
        {
            if (id == p.FindFirstValue(ClaimTypes.NameIdentifier)) return Bad("You cannot remove your own account.");
            var (ok, error) = await users.DeleteAsync(id);
            return ok ? Results.NoContent() : Bad(error ?? "Could not remove the user.");
        });
    }

    /// <summary>True when the stored section has both a meta title and description.</summary>
    static bool SeoComplete(string? json)
    {
        if (json == null) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("seo", out var seo)) return false;
            return !string.IsNullOrWhiteSpace(seo.GetProperty("title").GetString()) && !string.IsNullOrWhiteSpace(seo.GetProperty("description").GetString());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return false; }
    }
}
