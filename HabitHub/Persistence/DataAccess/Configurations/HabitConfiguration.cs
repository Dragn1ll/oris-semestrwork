using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.DataAccess.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<HabitEntity>
{
    public void Configure(EntityTypeBuilder<HabitEntity> builder)
    {
        builder.Property(h => h.Type)
            .HasConversion<string>();
            
        builder.Property(h => h.PhysicalActivityType)
            .HasConversion<string>();
    }
}