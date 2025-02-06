using ChessResultsStats_CSharp.Model;
using ChessResultsStats_CSharp.Service;
using Microsoft.AspNetCore.Mvc;

namespace ChessResultsStats_CSharp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly GamesService _gamesService;

    public GamesController(GamesService gamesService)
    {
        _gamesService = gamesService;
    }

    // GET: api/games?playerUsername=someusername
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Game>>> GetGamesByPlayerUsername([FromQuery] string playerUsername)
    {
        if (string.IsNullOrEmpty(playerUsername))
        {
            return BadRequest("playerUsername is required.");
        }

        // 1. Date de la dernière partie stockée en mémoire
        DateTime lastGameDateAndTime = _gamesService.GetLastGameDateAndTime(playerUsername);

        // 2. Récupère les données de l’API
        List<string> dataList = await _gamesService.GetGamesFromChessComAsync(
            playerUsername,
            lastGameDateAndTime,
            3);

        // 3. Formatte la liste de parties
        List<Game> currentGamesList = _gamesService.CreateFormattedGamesList(
            dataList,
            playerUsername,
            lastGameDateAndTime);

        // 4. Sauvegarde en mémoire (remplace la DB)
        await _gamesService.SaveGameInDatabaseAsync(currentGamesList);

        // 5. Retourne toutes les parties en mémoire pour cet utilisateur
        var allGamesForUser = _gamesService.GetGames(playerUsername);

        return Ok(allGamesForUser);
    }

    [HttpGet("Winrates")]
    public async Task<IActionResult> GetWinrates([FromQuery] string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Username is required.");
        }

        var winrates = await _gamesService.GetWinratesByHourAsync(username);
        if (winrates == null)
        {
            return NotFound($"No games found for user {username} in memory.");
        }
        return Ok(winrates);
    }

    [HttpGet("Get-Average-Moves")]
    public async Task<IActionResult> UpdateAverageMoves([FromQuery] string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Username is required.");
        }

        try
        {
            var result = await _gamesService.UpdateAverageMovesByPieceAsync(username);
            if (result == null)
            {
                return NotFound($"No games found for user {username} in memory.");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
