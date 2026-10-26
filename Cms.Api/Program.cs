using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Bjs.Cms.Api.Data;
using Bjs.Cms.Api.Endpoints;
using Bjs.Cms.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
#if !NO_EF
using Bjs.Cms.Api.Data.Ef;
using Microsoft.EntityFrameworkCore;
#endif

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var env = builder.Environment;

// ------------------------------------------------------------------ storage

var appData = Path.Combine(env.ContentRootPath, "App_Data");
Directory.CreateDirectory(appData);

#if NO_EF
builder.Services.AddSingleton<IDocumentStore>(new FileDocumentStore(
    ResolvePath(config["Database:FilePath"] ?? "App_Data/cms-dev.json")));
#else
var provider = (config["Database:Provider"] ?? "Sqlite").Trim();
if (provider.Equals("File", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStore>(new FileDocumentStore(ResolvePath(config["Database:FilePath"] ?? "App_Data/cms-dev.json")));
}
else
{
    var connection = config.GetConnectionString("Cms") ?? "Data Source=App_Data/cms.db";
    builder.Services.AddDbContextFactory<CmsDbContext>(o =>
    {
        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) o.UseSqlServer(connection);
        else o.UseSqlite(AnchorSqlite(connection));
    });
    builder.Services.AddSingleton<IDocumentStore, EfDocumentStore>();
}
#endif

string ResolvePath(string p) => Path.IsPathRooted(p) ? p : Path.Combine(env.ContentRootPath, p);

// A relative SQLite path would otherwise depend on the process's working directory (which differs under IIS).
string AnchorSqlite(string cs)
{
    const string prefix = "Data Source=";
    if (!cs.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return cs;
    var path = cs[prefix.Length..].Trim();
    return prefix + (Path.IsPathRooted(path) || path.StartsWith(':') ? path : Path.Combine(env.ContentRootPath, path));
}

builder.Services.AddSingleton<ContentService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<MediaService>();
builder.Services.AddSingleton<EnquiryService>();
builder.Services.AddSingleton<MailService>();
builder.Services.AddHttpClient("mailjet", c => c.Timeout = TimeSpan.FromSeconds(20));

// ------------------------------------------------------------------ security

builder.Services.AddDataProtection()
    .SetApplicationName("Bjs.Cms")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(appData, "keys")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "bjs.cms";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
        o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        // Removing a user, or changing their role, signs them out on their next request.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var users = ctx.HttpContext.RequestServices.GetRequiredService<UserService>();
            var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id == null ? null : await users.GetAsync(id);
            if (user == null || user.Role != ctx.Principal!.FindFirstValue(ClaimTypes.Role))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Only trust X-Forwarded-* when the host says there is a reverse proxy in front.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (config.GetValue<bool>("ForwardedHeaders:TrustAll")) { o.KnownNetworks.Clear(); o.KnownProxies.Clear(); }
});

// The website forwards the visitor's address in X-Client-IP; honour it only when the request carries the shared site key.
string ClientKey(HttpContext ctx)
{
    var expected = config["Site:ApiKey"];
    var forwarded = ctx.Request.Headers["X-Client-IP"].ToString();
    if (!string.IsNullOrEmpty(expected) && forwarded.Length is > 0 and < 64 && SiteKey.Matches(ctx, expected)) return forwarded;
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync("{\"successful\":false,\"message\":\"Too many requests. Please wait a few minutes and try again.\"}", ct);
    };
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("contact", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (origins.Length > 0)
    builder.Services.AddCors(o => o.AddPolicy("public", p => p.WithOrigins(origins).WithMethods("GET", "POST").WithHeaders("Content-Type", "If-None-Match")));

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 40 * 1024 * 1024);

// ------------------------------------------------------------------ pipeline

var app = builder.Build();

if (config.GetValue<bool>("ForwardedHeaders:Enabled")) app.UseForwardedHeaders();

if (!env.IsDevelopment())
{
    app.UseExceptionHandler(a => a.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync("{\"message\":\"Something went wrong on the server.\"}");
    }));
    if (config.GetValue("Https:Redirect", true)) { app.UseHsts(); app.UseHttpsRedirection(); }
}

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "same-origin";
    if (ctx.Request.Path.StartsWithSegments("/admin") || ctx.Request.Path.StartsWithSegments("/api"))
    {
        h["X-Frame-Options"] = "DENY";
        if (ctx.Request.Path.StartsWithSegments("/admin"))
            h["Content-Security-Policy"] =
                "default-src 'self'; img-src 'self' data: blob: https:" + (env.IsDevelopment() ? " http:" : "") + "; style-src 'self' 'unsafe-inline'; script-src 'self'; " +
                "frame-src https://www.google.com https://www.openstreetmap.org; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    }
    await next();
});

// Browsers send this custom header only from our own pages, which blocks cross-site form posts against the admin API.
app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path;
    var m = ctx.Request.Method;
    var unsafeMethod = !(HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m));
    if (unsafeMethod && (p.StartsWithSegments("/api/admin") || p.StartsWithSegments("/api/auth")) &&
        ctx.Request.Headers["X-Requested-With"] != "bjs-cms")
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { message = "Request blocked." });
        return;
    }
    await next();
});

// "/admin" -> "/admin/" (exact match only; routing treats both as the same route, so it cannot be done with MapGet)
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.Value == "/admin") { ctx.Response.Redirect("/admin/"); return; }
    await next();
});

// Uploaded images. Names are random, so they can be cached for a year.
var media = app.Services.GetRequiredService<MediaService>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(media.Root),
    RequestPath = "/uploads",
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        ctx.Context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        ctx.Context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
    }
});

// The dashboard (built React app).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path;
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = path.StartsWithSegments("/admin/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }
});

if (origins.Length > 0) app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/admin/"));
app.MapPublic();
app.MapAuth();
app.MapAdmin();
app.MapFallbackToFile("/admin/{*path:nonfile}", "admin/index.html");

// ------------------------------------------------------------------ first run

await using (var scope = app.Services.CreateAsyncScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
    await store.InitializeAsync();
    await scope.ServiceProvider.GetRequiredService<ContentService>().EnsureSeededAsync();
    await scope.ServiceProvider.GetRequiredService<UserService>().EnsureSeedAdminAsync();
}

app.Run();

public partial class Program { }
