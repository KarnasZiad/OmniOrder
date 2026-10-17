using Microsoft.EntityFrameworkCore;
using OmniOrder.Inventory.Api.Domain;

namespace OmniOrder.Inventory.Api.Data;

public static class InventorySeed
{
    /// <summary>Same SKUs as the Catalog demo data.</summary>
    public static readonly string[] DemoSkus =
        ["TSH-001", "JEA-002", "SWE-003", "VES-004", "CHE-005", "SNK-006", "BOT-007", "SAC-008", "CEI-009", "CAS-010"];

    public static async Task SeedAsync(InventoryDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Stores.AnyAsync(cancellationToken))
        {
            return;
        }

        var stores = new[]
        {
            new Store(Guid.Parse("8a1f0e2c-0001-4c6a-9d1e-000000000001"), "PAR-RIV", "Paris Rivoli", "Paris", "120 rue de Rivoli"),
            new Store(Guid.Parse("8a1f0e2c-0002-4c6a-9d1e-000000000002"), "LYO-PDI", "Lyon Part-Dieu", "Lyon", "17 rue du Docteur Bouchut"),
            new Store(Guid.Parse("8a1f0e2c-0003-4c6a-9d1e-000000000003"), "MRS-TDP", "Marseille Terrasses du Port", "Marseille", "9 quai du Lazaret"),
            new Store(Guid.Parse("8a1f0e2c-0004-4c6a-9d1e-000000000004"), "BDX-STC", "Bordeaux Sainte-Catherine", "Bordeaux", "45 rue Sainte-Catherine"),
            new Store(Guid.Parse("8a1f0e2c-0005-4c6a-9d1e-000000000005"), "LIL-EUR", "Lille Euralille", "Lille", "100 centre Euralille"),
        };
        db.Stores.AddRange(stores);

        var random = new Random(42);
        foreach (var store in stores)
        {
            foreach (var sku in DemoSkus)
            {
                // A few empty shelves so that "not available in this store" shows up in the demo.
                db.StockLevels.Add(new StockLevel(store.Id, sku, random.Next(0, 6) == 0 ? 0 : random.Next(1, 15)));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
