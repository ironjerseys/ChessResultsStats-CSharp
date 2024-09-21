using Microsoft.EntityFrameworkCore;
using ChessResultsStats_CSharp.Model;

namespace ChessResultsStats_CSharp.Data;

public class ChessGamesDbContext : DbContext
{
    public ChessGamesDbContext(DbContextOptions<ChessGamesDbContext> options) : base(options) { }

    public DbSet<Game> Games { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurer la table 'Games' si besoin (facultatif)
        modelBuilder.Entity<Game>()
            .ToTable("Games")
            .HasKey(g => g.Id); // Clé primaire
    }
}

// Self_Destruction_Lets_Go