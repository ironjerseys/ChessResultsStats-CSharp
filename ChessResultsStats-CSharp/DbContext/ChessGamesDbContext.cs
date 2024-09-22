using Microsoft.EntityFrameworkCore;
using ChessResultsStats_CSharp.Model;

namespace ChessResultsStats_CSharp.Data;

public class ChessGamesDbContext : DbContext
{
    public ChessGamesDbContext(DbContextOptions<ChessGamesDbContext> options) : base(options) { }

    public DbSet<Game> Games { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>().ToTable("Games").HasKey(g => g.Id); // Clé primaire
    }
}

// Self_Destruction_Lets_Go