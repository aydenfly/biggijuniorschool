using System.Text.Json;

namespace Bjs.Cms.Api.Data;

/// <summary>
/// DEV / TEST ONLY. Keeps every document in one JSON file. Handy for trying the CMS without a database;
/// use the EF Core store (SQLite / SQL Server) for real sites.
/// </summary>
public sealed class FileDocumentStore : IDocumentStore
{
    readonly string _path;
    readonly SemaphoreSlim _gate = new(1, 1);
    Dictionary<string, StoredDoc> _docs = new();

    public FileDocumentStore(string path) { _path = path; }

    static string Id(string c, string k) => c + "\u001f" + k;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_path))
            {
                var list = JsonSerializer.Deserialize<List<StoredDoc>>(await File.ReadAllTextAsync(_path, ct)) ?? new();
                _docs = list.ToDictionary(d => Id(d.Collection, d.Key));
            }
        }
        finally { _gate.Release(); }
    }

    Task SaveAsync(CancellationToken ct) =>
        File.WriteAllTextAsync(_path, JsonSerializer.Serialize(_docs.Values.ToList()), ct);

    public async Task<StoredDoc?> GetAsync(string collection, string key, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return _docs.GetValueOrDefault(Id(collection, key)); }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<StoredDoc>> ListAsync(string collection, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { return _docs.Values.Where(d => d.Collection == collection).ToList(); }
        finally { _gate.Release(); }
    }

    public async Task PutAsync(string collection, string key, string json, string? updatedBy, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _docs[Id(collection, key)] = new StoredDoc(collection, key, json, DateTime.UtcNow, updatedBy);
            await SaveAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(string collection, string key, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var removed = _docs.Remove(Id(collection, key));
            if (removed) await SaveAsync(ct);
            return removed;
        }
        finally { _gate.Release(); }
    }
}
