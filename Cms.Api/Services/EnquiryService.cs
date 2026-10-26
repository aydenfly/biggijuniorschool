using System.Text.Json;
using Bjs.Cms.Api.Data;

namespace Bjs.Cms.Api.Services;

public class Enquiry
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public bool Read { get; set; }
    /// <summary>sent | failed | not-configured</summary>
    public string Delivery { get; set; } = "";
}

public sealed class EnquiryService
{
    readonly IDocumentStore _store;
    public EnquiryService(IDocumentStore store) { _store = store; }

    static Enquiry? Read(StoredDoc d) => JsonSerializer.Deserialize<Enquiry>(d.Json, Json.Options);
    Task Write(Enquiry e) => _store.PutAsync(Collections.Enquiries, e.Id, JsonSerializer.Serialize(e, Json.Options), null);

    public async Task<IReadOnlyList<Enquiry>> ListAsync() =>
        (await _store.ListAsync(Collections.Enquiries)).Select(Read).Where(e => e != null).Select(e => e!).OrderByDescending(e => e.CreatedAtUtc).ToList();

    public async Task<Enquiry> AddAsync(string name, string email, string message, string delivery)
    {
        var e = new Enquiry { Id = Guid.NewGuid().ToString("N"), FullName = name, Email = email, Message = message, CreatedAtUtc = DateTime.UtcNow, Delivery = delivery };
        await Write(e);
        return e;
    }

    public async Task UpdateDeliveryAsync(string id, string delivery)
    {
        var d = await _store.GetAsync(Collections.Enquiries, id);
        var e = d == null ? null : Read(d);
        if (e == null) return;
        e.Delivery = delivery;
        await Write(e);
    }

    public async Task<Enquiry?> SetReadAsync(string id, bool read)
    {
        var d = await _store.GetAsync(Collections.Enquiries, id);
        var e = d == null ? null : Read(d);
        if (e == null) return null;
        e.Read = read;
        await Write(e);
        return e;
    }

    public Task<bool> DeleteAsync(string id) => _store.DeleteAsync(Collections.Enquiries, id);
}
