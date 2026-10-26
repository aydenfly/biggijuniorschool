namespace Bjs.Cms.Api.Data;

/// <summary>One stored JSON document.</summary>
public record StoredDoc(string Collection, string Key, string Json, DateTime UpdatedAtUtc, string? UpdatedBy);

/// <summary>
/// The CMS keeps everything (page content, media records, enquiries, users) as small JSON documents
/// grouped into collections. This is the only thing the rest of the app knows about the database.
/// </summary>
public interface IDocumentStore
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<StoredDoc?> GetAsync(string collection, string key, CancellationToken ct = default);
    Task<IReadOnlyList<StoredDoc>> ListAsync(string collection, CancellationToken ct = default);
    Task PutAsync(string collection, string key, string json, string? updatedBy, CancellationToken ct = default);
    Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default);
}

public static class Collections
{
    public const string Content = "content";
    public const string Media = "media";
    public const string Enquiries = "enquiries";
    public const string Users = "users";
}
