using CityQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityQuest.DataAccess.Configurations;

public class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Category);
        builder.Property(x => x.Fact).HasMaxLength(2000);
        builder.Property(x => x.Challenge).HasMaxLength(1000);
        builder.Property(x => x.Hint).HasMaxLength(1000);
        builder.ToTable("Places", table =>
        {
            table.HasCheckConstraint("CK_Places_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            table.HasCheckConstraint("CK_Places_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_Places_VisitMinutes", "\"VisitMinutes\" BETWEEN 1 AND 1440");
            table.HasCheckConstraint("CK_Places_Category", "\"Category\" IN ('architecture', 'nature', 'monument', 'museum', 'cafe', 'restaurant')");
        });
    }
}
