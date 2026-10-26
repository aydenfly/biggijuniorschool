using Microsoft.EntityFrameworkCore;

namespace Bjs.Cms.Api.Data.Ef;

/// <summary>EF Core implementation (SQLite or SQL Server). Registered as a singleton on top of a DbContext factory.</summary>
public sealed class EfDocumentStore : IDocumentStore
{
    readonly IDbContextFactory<CmsDbContext> _factory;

    public EfDocumentStore(IDbContextFactory<CmsDbContext> factory) { _factory = factory; }

    static StoredDoc Map(DocumentEntity e) => new(e.Collection, e.Key, e.Json, e.UpdatedAtUtc, e.UpdatedBy);

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
    }

    public async Task<StoredDoc?> GetAsync(string collection, string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Collection == collection && d.Key == key, ct);
        return e == null ? null : Map(e);
    }

    public async Task<IReadOnlyList<StoredDoc>> ListAsync(string collection, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Documents.AsNoTracking().Where(d => d.Collection == collection).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task PutAsync(string collection, string key, string json, string? updatedBy, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Documents.FirstOrDefaultAsync(d => d.Collection == collection && d.Key == key, ct);
        if (e == null)
        {
            e = new DocumentEntity { Collection = collection, Key = key };
            db.Documents.Add(e);
        }
        e.Json = json;
        e.UpdatedAtUtc = DateTime.UtcNow;
        e.UpdatedBy = updatedBy;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.Documents.FirstOrDefaultAsync(d => d.Collection == collection && d.Key == key, ct);
        if (e == null) return false;
        db.Documents.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
