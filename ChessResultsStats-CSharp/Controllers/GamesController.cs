using ChessResultsStats_CSharp.Data;
using ChessResultsStats_CSharp.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChessResultsStats_CSharp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly ChessGamesDbContext _context;

    public GamesController(ChessGamesDbContext context)
    {
        _context = context;
    }

    // GET: api/games?username=someusername
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Game>>> GetGamesByPlayerUsername(string playerUsername)
    {
        var games = await _context.Games
            .Where(g => g.PlayerUsername == playerUsername)
            .ToListAsync();

        if (games == null || games.Count == 0)
        {
            return NotFound();
        }

        return Ok(games);
    }
}
