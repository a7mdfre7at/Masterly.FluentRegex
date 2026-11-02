using Microsoft.EntityFrameworkCore;

namespace Masterly.FluentRegex.EntityFrameworkCore.Tests;

public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Email { get; set; }
}

public sealed class Order
{
    public int Id { get; set; }

    [MatchesPattern(typeof(TestPatterns), nameof(TestPatterns.Sku))]
    public string? Sku { get; set; }
}

/// <summary>Products without constraints.</summary>
public sealed class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
}

/// <summary>Products whose Sku column is non-Unicode (varchar), as in many SQL Server schemas.</summary>
public sealed class VarcharCatalogContext : DbContext
{
    public VarcharCatalogContext(DbContextOptions<VarcharCatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Product>().Property(product => product.Sku).IsUnicode(false).HasMaxLength(20);
}

/// <summary>Products whose Sku column carries a HasPattern CHECK constraint.</summary>
public sealed class ConstrainedCatalogContext : DbContext
{
    public const string ConstraintName = "CK_Products_Sku_Pattern";

    public ConstrainedCatalogContext(DbContextOptions<ConstrainedCatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The container scenarios seed explicit ids, which identity columns would reject.
        modelBuilder.Entity<Product>().Property(product => product.Id).ValueGeneratedNever();
        modelBuilder.Entity<Product>().Property(product => product.Sku).HasPattern(TestPatterns.Sku);
    }
}

/// <summary>Products whose Sku constraint has an explicit name.</summary>
public sealed class NamedConstraintCatalogContext : DbContext
{
    public const string ConstraintName = "CK_Custom_Sku";

    public NamedConstraintCatalogContext(DbContextOptions<NamedConstraintCatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(product => product.Sku).HasPattern(TestPatterns.Sku, ConstraintName);
    }
}

/// <summary>Orders whose Sku property is annotated with [MatchesPattern].</summary>
public sealed class AttributeCatalogContext : DbContext
{
    public const string ConstraintName = "CK_Orders_Sku_Pattern";

    public AttributeCatalogContext(DbContextOptions<AttributeCatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
}

public static class TestData
{
    public static List<Product> Products() => new List<Product>
    {
        new Product { Id = 1, Name = "Alpha 1", Sku = "ABC-1234", Email = "alpha@example.com" },
        new Product { Id = 2, Name = "beta", Sku = "abc-1234", Email = "beta@example" },
        new Product { Id = 3, Name = "Gamma 22", Sku = "XYZ-0001", Email = "gamma.ray@example.org" },
        new Product { Id = 4, Name = "Delta", Sku = null, Email = null },
        new Product { Id = 5, Name = "Epsilon 3", Sku = "ABCD-12345", Email = "not an email" },
        new Product { Id = 6, Name = "Zeta", Sku = "ABC-12345", Email = "zeta+tag@example.co.uk" },
        new Product { Id = 7, Name = "Eta 7", Sku = "ABC-0007", Email = "eta@example.com" }
    };

    public static List<int> IdsMatching(Func<Product, string?> selector, Func<string, bool> isMatch) =>
        Products().Where(product => selector(product) is string value && isMatch(value)).Select(product => product.Id).ToList();
}
