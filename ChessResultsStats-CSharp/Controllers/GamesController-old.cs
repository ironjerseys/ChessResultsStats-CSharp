//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using ChessResultsStats_CSharp.Model;
//using ChessResultsStats_CSharp.Service;
//using Microsoft.AspNetCore.Cors;
//using ChessResultsStats_CSharp.Model;
//using ChessResultsStats_CSharp.Service;

//namespace ChessResultsStats_CSharp.Controllers;

//[ApiController]
//[Route("[controller]")]
//[EnableCors("AllowAllOrigins")] // Vous pouvez définir une politique CORS dans Startup.cs
//public class GamesController : ControllerBase
//{
//    private readonly GamesService _gamesService;

//    public GamesController(GamesService gamesService)
//    {
//        _gamesService = gamesService;
//    }

//    [HttpGet("games")]
//    public async Task<ActionResult<List<Game>>> GetGames([FromQuery] string username)
//    {
//        // 1. On vérifie la date du dernier jeu dans la base de données
//        DateTime lastGameDateAndTime = await _gamesService.GetLastGameDateAndTimeAsync(username);

//        // 2. On récupère les données de l'API de chess.com, chaque chaîne dans la liste représente un mois de données
//        List<string> dataList = await _gamesService.GetGamesFromChessComAsync(username, lastGameDateAndTime, 3);

//        // 3. On crée une liste de jeux récents avec les données
//        List<Game> currentGamesList = _gamesService.CreateFormattedGamesList(dataList, username, lastGameDateAndTime);

//        // 4. On enregistre cette liste dans la base de données
//        await _gamesService.SaveGameInDatabaseAsync(currentGamesList);

//        // 5. On renvoie tous les jeux de cet utilisateur à l'interface utilisateur
//        var games = await _gamesService.GetGamesAsync(username);

//        return Ok(games);
//    }













//    [HttpPost("games")]
//    public async Task<IActionResult> AddGame([FromBody] List<Game> gameList)
//    {
//        await _gamesService.SaveGameInDatabaseAsync(gameList);
//        return Ok();
//    }
//}

