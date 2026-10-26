using System.Collections.Concurrent;
using System.Net.Mail;
using System.Text.Json;
using Bjs.Cms.Api.Data;

namespace Bjs.Cms.Api.Services;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public static bool IsValid(string? r) => r == Admin || r == Editor;
}

public class AdminUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = Roles.Editor;
    public string PasswordHash { get; set; } = "";
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}

public sealed class UserService
{
    readonly IDocumentStore _store;
    readonly IConfiguration _config;
    readonly ILogger<UserService> _log;
    readonly ConcurrentDictionary<string, (int Fails, DateTime LockedUntil)> _attempts = new();

    public UserService(IDocumentStore store, IConfiguration config, ILogger<UserService> log)
    {
        _store = store; _config = config; _log = log;
    }

    static AdminUser? Read(StoredDoc? d) => d == null ? null : JsonSerializer.Deserialize<AdminUser>(d.Json, Json.Options);
    Task Write(AdminUser u) => _store.PutAsync(Collections.Users, u.Id, JsonSerializer.Serialize(u, Json.Options), u.Email);

    public async Task<IReadOnlyList<AdminUser>> ListAsync() =>
        (await _store.ListAsync(Collections.Users)).Select(Read).Where(u => u != null).Select(u => u!).OrderBy(u => u.Name).ToList();

    public async Task<AdminUser?> GetAsync(string id) => Read(await _store.GetAsync(Collections.Users, id));

    public async Task<AdminUser?> FindByEmailAsync(string email) =>
        (await ListAsync()).FirstOrDefault(u => u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Creates the first administrator when there are no users. Password comes from config or is generated and logged once.</summary>
    public async Task EnsureSeedAdminAsync()
    {
        if ((await ListAsync()).Count > 0) return;
        var email = _config["Admin:Email"] is { Length: > 0 } e ? e : "admin@example.com";
        var configured = _config["Admin:Password"];
        var password = string.IsNullOrWhiteSpace(configured) ? PasswordHasher.Generate() : configured;
        await Write(new AdminUser
        {
            Id = Guid.NewGuid().ToString("N"), Email = email, Name = "Administrator", Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash(password), MustChangePassword = string.IsNullOrWhiteSpace(configured),
            CreatedAtUtc = DateTime.UtcNow,
        });
        if (string.IsNullOrWhiteSpace(configured))
            _log.LogWarning("FIRST-RUN ADMIN ACCOUNT CREATED. Sign in with {Email} and password: {Password}  (you will be asked to change it)", email, password);
        else
            _log.LogInformation("First administrator account created for {Email}.", email);
    }

    public static string? PasswordProblem(string password)
    {
        if (password.Length < 10) return "Use at least 10 characters.";
        if (password.Length > 128) return "Use 128 characters or fewer.";
        if (password.Distinct().Count() < 4) return "Use a less repetitive password.";
        return null;
    }

    /// <summary>Returns the user on success. Locks the email for 10 minutes after 5 consecutive failures.</summary>
    public async Task<(AdminUser? User, string? Error)> SignInAsync(string email, string password)
    {
        var key = email.Trim().ToLowerInvariant();
        if (_attempts.TryGetValue(key, out var a) && a.LockedUntil > DateTime.UtcNow)
            return (null, "Too many attempts. Try again in a few minutes.");

        var user = await FindByEmailAsync(email);
        // verify against a dummy hash when the user does not exist, so timing does not reveal valid emails
        var ok = user != null ? PasswordHasher.Verify(password, user.PasswordHash) : PasswordHasher.Verify(password, DummyHash);
        if (!ok || user == null)
        {
            var fails = (_attempts.TryGetValue(key, out var prev) ? prev.Fails : 0) + 1;
            _attempts[key] = (fails, fails >= 5 ? DateTime.UtcNow.AddMinutes(10) : default);
            return (null, "That email and password do not match.");
        }
        _attempts.TryRemove(key, out _);
        user.LastLoginUtc = DateTime.UtcNow;
        await Write(user);
        return (user, null);
    }

    static readonly string DummyHash = PasswordHasher.Hash("not-a-real-password");

    public async Task<(AdminUser? User, string? Error)> CreateAsync(string email, string name, string role, string password)
    {
        email = email.Trim(); name = name.Trim();
        try { _ = new MailAddress(email); } catch (FormatException) { return (null, "Enter a valid email address."); }
        if (name.Length is 0 or > 100) return (null, "Enter a name (100 characters or fewer).");
        if (!Roles.IsValid(role)) return (null, "Choose a role.");
        if (PasswordProblem(password) is { } p) return (null, p);
        if (await FindByEmailAsync(email) != null) return (null, "A user with that email already exists.");
        var u = new AdminUser
        {
            Id = Guid.NewGuid().ToString("N"), Email = email, Name = name, Role = role,
            PasswordHash = PasswordHasher.Hash(password), MustChangePassword = true, CreatedAtUtc = DateTime.UtcNow,
        };
        await Write(u);
        return (u, null);
    }

    public async Task SetPasswordAsync(AdminUser user, string newPassword, bool mustChange)
    {
        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = mustChange;
        await Write(user);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(AdminUser user, string? name, string? role)
    {
        if (name != null) { if (name.Trim().Length is 0 or > 100) return (false, "Enter a name (100 characters or fewer)."); user.Name = name.Trim(); }
        if (role != null)
        {
            if (!Roles.IsValid(role)) return (false, "Choose a role.");
            if (user.Role == Roles.Admin && role != Roles.Admin && (await ListAsync()).Count(u => u.Role == Roles.Admin) <= 1)
                return (false, "There must be at least one administrator.");
            user.Role = role;
        }
        await Write(user);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(string id)
    {
        var user = await GetAsync(id);
        if (user == null) return (false, "User not found.");
        if (user.Role == Roles.Admin && (await ListAsync()).Count(u => u.Role == Roles.Admin) <= 1)
            return (false, "You cannot remove the only administrator.");
        await _store.DeleteAsync(Collections.Users, id);
        return (true, null);
    }
}
