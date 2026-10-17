using Microsoft.EntityFrameworkCore;
using OmniOrder.Catalog.Api.Domain;

namespace OmniOrder.Catalog.Api.Data;

public static class CatalogSeed
{
    /// <summary>Demo catalogue. Inventory seeds stock for the same SKUs.</summary>
    public static async Task SeedAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        db.Products.AddRange(
            new Product("TSH-001", "T-shirt coton bio", "Coupe droite, coton biologique 180 g/m2.", "Textile", 19.90m, now),
            new Product("JEA-002", "Jean slim brut", "Denim stretch, coupe slim, lavage brut.", "Textile", 59.90m, now),
            new Product("SWE-003", "Sweat a capuche", "Molleton gratte, poche kangourou.", "Textile", 44.90m, now),
            new Product("VES-004", "Veste en jean", "Veste trucker, denim delave.", "Textile", 79.90m, now),
            new Product("CHE-005", "Chemise en lin", "Lin lave, col francais.", "Textile", 49.90m, now),
            new Product("SNK-006", "Sneakers cuir blanc", "Cuir pleine fleur, semelle cousue.", "Chaussures", 89.90m, now),
            new Product("BOT-007", "Bottines chelsea", "Daim, elastiques lateraux.", "Chaussures", 119.00m, now),
            new Product("SAC-008", "Sac cabas toile", "Toile de coton epaisse, anses cuir.", "Accessoires", 34.90m, now),
            new Product("CEI-009", "Ceinture cuir", "Cuir tanne vegetal, boucle laiton.", "Accessoires", 29.90m, now),
            new Product("CAS-010", "Casquette brodee", "Coton serge, logo brode.", "Accessoires", 24.90m, now));

        await db.SaveChangesAsync(cancellationToken);
    }
}
