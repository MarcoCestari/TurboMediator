using TurboMediator;

namespace Sample.Observability;

// =============================================================
// MODELS
// =============================================================

public record ProductDto(
    Guid Id, string Name, string Description, string Category,
    decimal Price, int StockQuantity, string Seller, DateTime CreatedAt);

public record ProductSearchResult(
    IReadOnlyList<ProductDto> Items, int TotalCount, int Page, int PageSize);

// =============================================================
// IN-MEMORY STORE
// =============================================================

public static class ProductStore
{
    public static readonly Dictionary<Guid, ProductDto> Products = new();

    public static void Seed()
    {
        var products = new[]
        {
            Create("iPhone 15 Pro Max", "Smartphone Apple 256GB", "Smartphones", 9499.00m, 50, "TechStore"),
            Create("Samsung Galaxy S24", "Smartphone Samsung 128GB", "Smartphones", 5999.00m, 120, "MegaCell"),
            Create("MacBook Pro M3", "Notebook Apple 14\" 16GB", "Notebooks", 18999.00m, 25, "TechStore"),
            Create("Dell XPS 15", "Notebook Dell i7 16GB", "Notebooks", 12499.00m, 40, "DellDirect"),
            Create("AirPods Pro 2", "Apple earbuds with noise cancellation", "Accessories", 2299.00m, 200, "TechStore"),
            Create("Xbox Series X", "Console Microsoft 1TB", "Games", 4499.00m, 30, "GameWorld"),
            Create("Monitor LG UltraFine 27\"", "Monitor 4K IPS", "Monitors", 3299.00m, 60, "LGStore"),
        };

        foreach (var p in products)
            Products[p.Id] = p;
    }

    private static ProductDto Create(string name, string desc, string cat, decimal price, int stock, string seller) =>
        new(Guid.NewGuid(), name, desc, cat, price, stock, seller, DateTime.UtcNow);
}

// =============================================================
// QUERY: Get product by ID
// =============================================================

public record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDto?>;

public class GetProductByIdHandler : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    public ValueTask<ProductDto?> Handle(GetProductByIdQuery query, CancellationToken ct)
    {
        ProductStore.Products.TryGetValue(query.ProductId, out var product);
        return new ValueTask<ProductDto?>(product);
    }
}

// =============================================================
// QUERY: Search products (with telemetry)
// =============================================================

public record SearchProductsQuery(
    string? SearchTerm, string? Category, int Page, int PageSize) : IQuery<ProductSearchResult>;

public class SearchProductsHandler : IQueryHandler<SearchProductsQuery, ProductSearchResult>
{
    public ValueTask<ProductSearchResult> Handle(SearchProductsQuery query, CancellationToken ct)
    {
        var products = ProductStore.Products.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(query.SearchTerm))
            products = products.Where(p =>
                p.Name.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(query.Category))
            products = products.Where(p =>
                p.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));

        var total = products.Count();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var items = products.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new ValueTask<ProductSearchResult>(
            new ProductSearchResult(items, total, page, pageSize));
    }
}

// =============================================================
// COMMAND: Create product
// =============================================================

public record CreateProductCommand(
    string Name, string Description, string Category,
    decimal Price, int StockQuantity, string Seller) : ICommand<ProductDto>;

public class CreateProductHandler : ICommandHandler<CreateProductCommand, ProductDto>
{
    public ValueTask<ProductDto> Handle(CreateProductCommand command, CancellationToken ct)
    {
        var product = new ProductDto(
            Guid.NewGuid(),
            command.Name,
            command.Description,
            command.Category,
            command.Price,
            command.StockQuantity,
            command.Seller,
            DateTime.UtcNow);

        ProductStore.Products[product.Id] = product;
        return new ValueTask<ProductDto>(product);
    }
}
