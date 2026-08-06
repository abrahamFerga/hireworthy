using Hireworthy.Hiring;
using Microsoft.EntityFrameworkCore;
using Plenipo.Core.Multitenancy;
using Plenipo.Modules.Sdk;

namespace Hireworthy.Hiring.Persistence;

/// <summary>
/// The hiring module's own persistence, in its own schema.
/// </summary>
/// <remarks>
/// Two things here are load-bearing and must never be "simplified":
/// <list type="number">
/// <item>It derives from <see cref="ModuleDbContext"/>, not <see cref="DbContext"/>. The platform's
/// audit interceptor covers only the platform's own context; a module context deriving straight
/// from <c>DbContext</c> silently persists <c>default(DateTimeOffset)</c> timestamps.</item>
/// <item><b>Every <see cref="ITenantOwned"/> entity declares its own <c>HasQueryFilter</c>.</b>
/// <c>PlatformDbContext</c> applies filters by reflection; a module context does not, so a new
/// entity added without a filter is a silent cross-tenant leak — the highest-consequence mistake
/// available in this codebase, and in this product it means one employer seeing another employer's
/// candidates. <c>HiringDbContextTests</c> fails the build if an entity is added without one.</item>
/// </list>
/// </remarks>
public sealed class HiringDbContext(
    DbContextOptions<HiringDbContext> options,
    ITenantContext tenantContext)
    : ModuleDbContext(options)
{
    public const string Schema = "hiring";

    /// <summary>
    /// The module's own migrations-history table, in the module's own schema.
    /// </summary>
    /// <remarks>
    /// It must NOT be the default <c>public.__EFMigrationsHistory</c>: the module shares a database
    /// with the platform, so a shared history table would let either side believe the other's
    /// migrations were its own and skip applying them.
    /// </remarks>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public DbSet<Requisition> Requisitions => Set<Requisition>();

    public DbSet<Rubric> Rubrics => Set<Rubric>();

    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Requisition>(entity =>
        {
            entity.ToTable("requisitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
            entity.Property(e => e.JobDescription).HasMaxLength(32_000);
            entity.Property(e => e.Location).HasMaxLength(256);
            entity.Property(e => e.HiringManager).HasMaxLength(256);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            // UNIQUE per organisation, and load-bearing rather than tidy: a reference identifies a
            // requisition within an employer, and the uniqueness is what makes a concurrent seed
            // (two hosts booting against one fresh database) fail loudly instead of silently
            // duplicating every req.
            entity.HasIndex(e => new { e.TenantId, e.Reference }).IsUnique();

            // The tenant boundary. One per ITenantOwned entity, always.
            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<Rubric>(entity =>
        {
            entity.ToTable("rubrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Rationale).HasMaxLength(4000);
            entity.Property(e => e.ApprovedBy).HasMaxLength(256);
            // A version may never be reused within a requisition: scores pin it, so a duplicate
            // would silently re-point historical scores at a different set of criteria.
            entity.HasIndex(e => new { e.RequisitionId, e.Version }).IsUnique();

            entity.HasOne(e => e.Requisition)
                .WithMany(r => r.Rubrics)
                .HasForeignKey(e => e.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<RubricCriterion>(entity =>
        {
            entity.ToTable("rubric_criteria");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Requirement).HasMaxLength(2000).IsRequired();

            entity.HasOne(e => e.Rubric)
                .WithMany(r => r.Criteria)
                .HasForeignKey(e => e.RubricId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });
    }
}
