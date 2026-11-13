using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    internal static readonly Guid FoodId = new("ca7e0000-0000-0000-0000-000000000001");
    internal static readonly Guid DrinksId = new("ca7e0000-0000-0000-0000-000000000002");

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("bil_categories");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(Product.MaximumNameLength);
        builder.HasData(new { Id = FoodId, Name = "Comidas" }, new { Id = DrinksId, Name = "Bebidas" });
    }
}
