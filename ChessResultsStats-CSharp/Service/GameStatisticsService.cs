using ChessResultsStats_CSharp.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessResultsStats_CSharp.Service;

public class GameStatisticsService
{
    private readonly ChessGamesDbContext _context;

    public GameStatisticsService(ChessGamesDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<DayOfWeek, double>> CalculateWinrateByDayAsync(string playerUsername)
    {
        // Filtre les parties par joueur et groupe-les par jour de la semaine
        var winrateByDay = await _context.Metadatas
            .Where(m => m.PlayerUsername == playerUsername)
            .GroupBy(m => m.DayOfWeek)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Count(m => m.ResultForPlayer == "won") * 100.0 / g.Count()
            );

        return winrateByDay;
    }
}
