using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PirryLedger.Business.Domain.Entities;

namespace PirryLedger.Business.Infrastructure.EntityConfigurations;

public sealed class SubcategoryConfiguration : IEntityTypeConfiguration<Subcategory>
{
    public void Configure(EntityTypeBuilder<Subcategory> builder)
    {
        builder.ToTable("bil_subcategories");
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.CategoryId).HasColumnName("category_id");
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(Product.MaximumNameLength);
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);

        string[] names = ["Chimis", "Club sándwiches", "Sándwiches", "Yaroas", "Hamburguesas",
            "Tostones", "Jugos naturales", "Batidas", "Refrescos", "Agua"];

        builder.HasData(names.Select((name, index) => new
        {
            Id = new Guid($"ca7e0000-0000-0000-0001-{index + 1:D12}"),
            CategoryId = index < 6 ? CategoryConfiguration.FoodId : CategoryConfiguration.DrinksId,
            Name = name
        }));
    }
}
