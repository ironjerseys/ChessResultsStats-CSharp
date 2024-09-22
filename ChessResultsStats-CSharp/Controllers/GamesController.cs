using ChessResultsStats_CSharp.Service;
using ChessResultsStats_CSharp.Model;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChessResultsStats_CSharp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly GamesService _gamesService;

    public GamesController(GamesService gamesService) // Injection du service
    {
        _gamesService = gamesService;
    }

    // GET: api/games?username=someusername
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Game>>> GetGamesByPlayerUsername(string playerUsername)
    {
        // Utilisation de la méthode GetLastGameDateAndTimeAsync du service
        DateTime lastGameDateAndTime = await _gamesService.GetLastGameDateAndTimeAsync(playerUsername);

        // Appelle la méthode pour récupérer les jeux du service
        var games = await _gamesService.GetGamesAsync(playerUsername);

        if (games == null || games.Count == 0)
        {
            return NotFound();
        }

        return Ok(games);
    }
}
