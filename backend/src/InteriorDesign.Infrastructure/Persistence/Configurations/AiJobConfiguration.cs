using InteriorDesign.Domain;
using InteriorDesign.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InteriorDesign.Infrastructure.Persistence.Configurations;

internal class AiJobConfiguration : IEntityTypeConfiguration<AiJob>
{
    public void Configure(EntityTypeBuilder<AiJob> b)
    {
        b.ToTable("ai_jobs", t =>
        {
            t.HasCheckConstraint("ck_ai_jobs_type", Check.In("type", DomainValues.JobTypes));
            t.HasCheckConstraint("ck_ai_jobs_status", Check.In("status", DomainValues.JobStatuses));
            t.HasCheckConstraint("ck_ai_jobs_progress", "progress >= 0 AND progress <= 100");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Type).HasMaxLength(20);
        b.Property(x => x.DesignId).HasMaxLength(64);
        b.Property(x => x.ResultDesignId).HasMaxLength(64);
        b.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("pendiente");
        b.Property(x => x.InputJson).HasColumnName("input").HasColumnType("jsonb");
        b.Property(x => x.ResultJson).HasColumnName("result").HasColumnType("jsonb");
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        b.HasIndex(x => x.DesignId);
    }
}
