using ChessResultsStats_CSharp.Model;
using Microsoft.EntityFrameworkCore;

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