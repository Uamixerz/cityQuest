using CityQuest.DataAccess.Identity;
using CityQuest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CityQuest.DataAccess.Configurations;

public class RouteConfiguration : IEntityTypeConfiguration<WalkingRoute>,
    IEntityTypeConfiguration<RouteStop>, IEntityTypeConfiguration<RouteLike>, IEntityTypeConfiguration<SavedRoute>
{
    public void Configure(EntityTypeBuilder<WalkingRoute> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CalculationMethod).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FoodType).HasMaxLength(50);
        builder.Property(x => x.GeometryJson).HasColumnType("jsonb");
        builder.Property(x => x.Attribution).HasMaxLength(1000);
        builder.HasIndex(x => new { x.IsPublic, x.CreatedAt });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Stops).WithOne().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Likes).WithOne().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Saves).WithOne().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable("Routes", table =>
            table.HasCheckConstraint("CK_Routes_TimeBudget", "\"TotalMinutes\" > 0 AND \"TotalMinutes\" <= \"TimeBudgetMinutes\""));
    }

    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.HasKey(x => new { x.RouteId, x.Position });
        builder.HasIndex(x => new { x.RouteId, x.PlaceId }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.HasOne<Place>().WithMany().HasForeignKey(x => x.PlaceId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<RouteLike> builder)
    {
        builder.HasKey(x => new { x.RouteId, x.UserId });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<SavedRoute> builder)
    {
        builder.HasKey(x => new { x.RouteId, x.UserId });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
