using Microsoft.EntityFrameworkCore;

namespace Bjs.Cms.Api.Data.Ef;

public class DocumentEntity
{
    public string Collection { get; set; } = "";
    public string Key { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CmsDbContext : DbContext
{
    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options) { }

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentEntity>(e =>
        {
            e.ToTable("CmsDocuments");
            e.HasKey(x => new { x.Collection, x.Key });
            e.Property(x => x.Collection).HasMaxLength(64);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.UpdatedBy).HasMaxLength(256);
        });
    }
}
