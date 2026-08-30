using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AffiliateSuperstore.Persistence;

public sealed class AffiliateSuperstoreDesignTimeFactory
    : IDesignTimeDbContextFactory<AffiliateSuperstoreDbContext>
{
    public AffiliateSuperstoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=AffiliateSuperstoreLocal;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new AffiliateSuperstoreDbContext(options);
    }
}
