using System.Text.Json;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bjs.Cms.Api.Services;
using Bjs.Cms.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Bjs.Cms.Api.Endpoints;

public static class PublicEndpoints
{
    static readonly Regex NamePattern = new(@"^[^\d_<>]+$", RegexOptions.Compiled);

    public static void MapPublic(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/public");
        if (app is WebApplication wa && wa.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 })
            g.RequireCors("public");

        g.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Everything the website needs, in one request. Supports conditional GET so the site can poll cheaply.
        g.MapGet("/content", async (HttpContext ctx, ContentService content) =>
        {
            var snap = await content.GetPublicAsync();
            ctx.Response.Headers.ETag = snap.ETag;
            ctx.Response.Headers.CacheControl = "public, max-age=15, must-revalidate";
            if (ctx.Request.Headers.IfNoneMatch.ToString().Contains(snap.ETag))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            return Results.Text(snap.Json, "application/json");
        });

        g.MapPost("/contact", async (HttpContext ctx, EnquiryRequest req, ContentService content, EnquiryService enquiries,
                                      MailService mail, IConfiguration config, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("Contact");

            // When Site:ApiKey is set, only the website's server may post here (it forwards the visitor's form).
            var expected = config["Site:ApiKey"];
            if (!string.IsNullOrEmpty(expected) && !SiteKey.Matches(ctx, expected))
                return Results.Json(new { successful = false, message = "Not allowed." }, statusCode: 401);

            // Honeypot: pretend everything worked so bots do not learn anything.
            if (!string.IsNullOrEmpty(req.HpToken))
                return Results.Ok(new { successful = true, message = "Thank you for your enquiry. We'll get back to you shortly." });

            var name = ContentNormalizer.CleanText(req.FullName ?? "", false);
            var email = (req.Email ?? "").Trim();
            var message = ContentNormalizer.CleanText(req.Message ?? "", true);

            var errors = new Dictionary<string, string>();
            if (name.Length == 0) errors["fullName"] = "Your name is required.";
            else if (name.Length > 50) errors["fullName"] = "Your name may contain no more than 50 characters.";
            else if (!NamePattern.IsMatch(name)) errors["fullName"] = "Name may only contain alphabetical characters.";
            if (email.Length == 0) errors["email"] = "Please enter your email address.";
            else if (email.Length > 254 || !IsEmail(email)) errors["email"] = "Please enter a valid email address.";
            if (message.Length == 0) errors["message"] = "Please enter a message.";
            else if (message.Length > 1000) errors["message"] = "Please keep your message under 1000 characters.";

            if (errors.Count > 0)
                return Results.Json(new { successful = false, message = "Please check the highlighted fields and try again.", errors }, statusCode: 400);

            var general = (await content.GetAsync("general"))?.Data.Deserialize<GeneralSettings>(Json.Options) ?? new GeneralSettings();
            var to = general.NotificationEmail;
            if (string.IsNullOrWhiteSpace(to)) to = config["Mail:DefaultTo"] ?? "";

            var saved = await enquiries.AddAsync(name, email, message, mail.IsConfigured && to.Length > 0 ? "pending" : "not-configured");

            if (mail.IsConfigured && to.Length > 0)
            {
                var siteUrl = config["Site:PublicUrl"] ?? "";
                var (html, text) = mail.Render(siteUrl, name, email, message, saved.CreatedAtUtc);
                var subject = string.IsNullOrWhiteSpace(general.NotificationSubject) ? "Online enquiry" : general.NotificationSubject;
                var ok = await mail.SendAsync(new MailMessageData(to, general.SiteName.Length > 0 ? general.SiteName + " admin" : "Website admin",
                    subject, html, text, email, name));
                await enquiries.UpdateDeliveryAsync(saved.Id, ok ? "sent" : "failed");
                if (!ok) log.LogError("Enquiry {Id} was saved but the notification email failed.", saved.Id);
            }

            // The visitor's message is safely stored either way, so they always get a success message.
            return Results.Ok(new { successful = true, message = "Thank you for your enquiry. We'll get back to you shortly." });
        }).RequireRateLimiting("contact");
    }

    static bool IsEmail(string s)
    {
        try { var a = new MailAddress(s); return a.Address == s && !s.Contains(' '); }
        catch (FormatException) { return false; }
    }
}

public static class SiteKey
{
    public static bool Matches(HttpContext ctx, string expected)
    {
        var given = ctx.Request.Headers["X-Site-Key"].ToString();
        if (given.Length != expected.Length) return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(given), System.Text.Encoding.UTF8.GetBytes(expected));
    }
}

public static class AuthEndpoints
{
    public record LoginRequest(string? Email, string? Password);
    public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

    public static object Me(AdminUser u) => new { id = u.Id, email = u.Email, name = u.Name, role = u.Role, mustChangePassword = u.MustChangePassword };

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/login", async (HttpContext ctx, LoginRequest req, UserService users) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
                return Results.Json(new { message = "Enter your email and password." }, statusCode: 400);

            var (user, error) = await users.SignInAsync(req.Email, req.Password);
            if (user == null) return Results.Json(new { message = error }, statusCode: 401);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Role, user.Role),
            }, CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok(Me(user));
        }).RequireRateLimiting("login");

        g.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        g.MapGet("/me", async (ClaimsPrincipal p, UserService users) =>
        {
            var id = p.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id == null ? null : await users.GetAsync(id);
            return user == null ? Results.Unauthorized() : Results.Ok(Me(user));
        }).RequireAuthorization();

        g.MapPost("/change-password", async (ClaimsPrincipal p, ChangePasswordRequest req, UserService users) =>
        {
            var user = await users.GetAsync(p.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            if (user == null) return Results.Unauthorized();
            if (string.IsNullOrEmpty(req.CurrentPassword) || !PasswordHasher.Verify(req.CurrentPassword, user.PasswordHash))
                return Results.Json(new { message = "Your current password is not correct." }, statusCode: 400);
            if (string.IsNullOrEmpty(req.NewPassword))
                return Results.Json(new { message = "Enter a new password." }, statusCode: 400);
            if (UserService.PasswordProblem(req.NewPassword) is { } problem)
                return Results.Json(new { message = problem }, statusCode: 400);
            if (req.NewPassword == req.CurrentPassword)
                return Results.Json(new { message = "Choose a password you have not used before." }, statusCode: 400);
            await users.SetPasswordAsync(user, req.NewPassword, mustChange: false);
            return Results.Ok(Me(user));
        }).RequireAuthorization();
    }
}
