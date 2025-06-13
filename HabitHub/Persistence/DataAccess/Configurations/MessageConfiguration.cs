using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.DataAccess.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        builder.HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Recipient)
            .WithMany(u => u.ReceivedMessages)
            .OnDelete(DeleteBehavior.Restrict);
    }
}