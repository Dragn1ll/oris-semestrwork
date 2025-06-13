using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.DataAccess.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.HasMany(u => u.ReceivedMessages)
            .WithOne(m => m.Recipient)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.SentMessages)
            .WithOne(m => m.Sender)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(u => u.Email)
            .IsUnique();
    }
}