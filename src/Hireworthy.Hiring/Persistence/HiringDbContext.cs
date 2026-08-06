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

    public DbSet<Applicant> Applicants => Set<Applicant>();

    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();

    public DbSet<ExtractedProfile> ExtractedProfiles => Set<ExtractedProfile>();

    public DbSet<EmploymentSpan> EmploymentSpans => Set<EmploymentSpan>();

    public DbSet<ScreeningResult> ScreeningResults => Set<ScreeningResult>();

    public DbSet<CriterionScore> CriterionScores => Set<CriterionScore>();

    public DbSet<Decision> Decisions => Set<Decision>();

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

        modelBuilder.Entity<Applicant>(entity =>
        {
            entity.ToTable("applicants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).HasMaxLength(64).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.Phone).HasMaxLength(64);
            entity.Property(e => e.Source).HasMaxLength(128);
            entity.Property(e => e.Stage).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.TenantId, e.Reference }).IsUnique();

            entity.HasOne(e => e.Requisition)
                .WithMany()
                .HasForeignKey(e => e.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<CvDocument>(entity =>
        {
            entity.ToTable("cv_documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            // No length cap on the CV text: citation offsets point into it, so a truncating column
            // would silently invalidate every span stored against it.
            entity.Property(e => e.ExtractedText).IsRequired();

            entity.HasOne(e => e.Applicant)
                .WithOne(a => a.Cv)
                .HasForeignKey<CvDocument>(e => e.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<ExtractedProfile>(entity =>
        {
            entity.ToTable("extracted_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Unresolved).HasMaxLength(4000);
            // Npgsql maps List<string> to text[] natively — no join table for what is a flat list
            // of things as written on the CV.
            entity.Property(e => e.Skills).HasColumnType("text[]");
            entity.Property(e => e.Education).HasColumnType("text[]");
            entity.Property(e => e.EmploymentGaps).HasColumnType("text[]");

            entity.HasOne(e => e.Applicant)
                .WithOne(a => a.Profile)
                .HasForeignKey<ExtractedProfile>(e => e.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<EmploymentSpan>(entity =>
        {
            entity.ToTable("employment_spans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Employer).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
            entity.Ignore(e => e.IsCurrent);

            entity.HasOne(e => e.Profile)
                .WithMany(p => p.Employment)
                .HasForeignKey(e => e.ExtractedProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<ScreeningResult>(entity =>
        {
            entity.ToTable("screening_results");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(e => e.Applicant)
                .WithMany()
                .HasForeignKey(e => e.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not Cascade: a rubric row must never be deletable out from under the scores
            // that pin its version. Losing the yardstick would make every score it produced
            // unexplainable, which is the one thing this product cannot allow.
            entity.HasOne(e => e.Rubric)
                .WithMany()
                .HasForeignKey(e => e.RubricId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<CriterionScore>(entity =>
        {
            entity.ToTable("criterion_scores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CriterionName).HasMaxLength(256).IsRequired();
            // No length cap: the citation is a verbatim span of the CV, and truncating it would
            // break the containment check that is the product's central guarantee.
            entity.Property(e => e.CitationText);
            entity.Property(e => e.Note).HasMaxLength(2000);

            entity.HasOne(e => e.ScreeningResult)
                .WithMany(r => r.Scores)
                .HasForeignKey(e => e.ScreeningResultId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });

        modelBuilder.Entity<Decision>(entity =>
        {
            entity.ToTable("decisions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.FromStage).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ToStage).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Reason).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.ApprovedBy).HasMaxLength(256);

            entity.HasOne(e => e.Applicant)
                .WithMany()
                .HasForeignKey(e => e.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: the evidence a decision rests on must never be deletable out from under it.
            // A decision whose evidence vanished is exactly the thing this product exists to prevent
            // — see ADR-0008 for how the tombstone survives content deletion instead.
            entity.HasOne(e => e.Evidence)
                .WithMany()
                .HasForeignKey(e => e.ScreeningResultId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => e.TenantId == tenantContext.TenantId);
        });
    }
}
