using ChessResultsStats_CSharp.Model;
using ChessResultsStats_CSharp.Service;
using Microsoft.AspNetCore.Mvc;

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
        // 1. We check the date of the last game in the database
        DateTime lastGameDateAndTime = await _gamesService.GetLastGameDateAndTimeAsync(playerUsername);

        // 2. We get the data from the chess.com API, each string in the list is a month of data returned by the API
        List<string> dataList = await _gamesService.GetGamesFromChessComAsync(playerUsername, lastGameDateAndTime, 3);

        // 3. We create a list of recent games with the data
        List<Game> currentGamesList = _gamesService.CreateFormattedGamesList(dataList, playerUsername, lastGameDateAndTime);

        // 4. We save the list in database
        await _gamesService.SaveGameInDatabaseAsync(currentGamesList);

        // 5. We return all games for this user to the frontend
        var games = await _gamesService.GetGamesAsync(playerUsername);

        return Ok(games);
    }


    [HttpPost("games")]
    public async Task<IActionResult> AddGame([FromBody] List<Game> gameList)
    {
        await _gamesService.SaveGameInDatabaseAsync(gameList);
        return Ok();
    }

    [HttpGet("winrates")]
    public async Task<IActionResult> GetWinrates(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Username is required.");
        }

        // Appelle le service pour mettre à jour les données
        //await _gamesService.GetWinratesByHourAsync(username);

        // Récupère les données mises à jour pour les renvoyer au front
        var winrates = await _gamesService.GetWinratesByHourAsync(username);

        return Ok(winrates); // Renvoie les winrates au front
    }


    [HttpGet("get-average-moves")]
    public async Task<IActionResult> UpdateAverageMoves([FromQuery] string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Username is required.");
        }
        try
        {
            var result = await _gamesService.UpdateAverageMovesByPieceAsync(username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
