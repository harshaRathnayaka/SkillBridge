using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SkillBridge.ApiService.Data;

// IDataProtectionKeyContext: Program.cs's PersistKeysToDbContext<ApplicationDbContext>() needs
// this interface's DataProtectionKeys DbSet to store the key ring in the same database as
// everything else, instead of a local file/volume the compute host doesn't provide.
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<ProfileReference> ProfileReferences => Set<ProfileReference>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.HasIndex(rt => new { rt.UserId, rt.DeviceId });
            entity.Property(rt => rt.ConcurrencyStamp).IsConcurrencyToken();
        });

        // Dashboard domain: plain FK columns with no navigation properties and no constraint
        // enforced against ApplicationUser, mirroring RefreshToken.UserId above — kept as
        // simple indexed columns rather than real joins, consistent across the whole domain.
        builder.Entity<Course>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.TeacherId);
        });

        builder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StudentId);
            entity.HasIndex(e => e.CourseId);
        });

        builder.Entity<Material>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.CourseId);
        });

        builder.Entity<JobPosting>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.HasIndex(j => j.EmployerId);
        });

        builder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.ApplicantId);
            entity.HasIndex(a => a.JobPostingId);
        });

        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(p => p.UserId);
        });

        builder.Entity<Skill>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.NormalizedName).IsUnique();
        });

        builder.Entity<UserSkill>(entity =>
        {
            entity.HasKey(us => us.Id);
            entity.HasIndex(us => new { us.UserId, us.SkillId }).IsUnique();
        });

        builder.Entity<ProfileReference>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.UserId);
        });
    }
}
