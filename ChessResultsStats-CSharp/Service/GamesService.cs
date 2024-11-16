using ChessResultsStats_CSharp.Data;
using ChessResultsStats_CSharp.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChessResultsStats_CSharp.Service;

public class GamesService
{
    private readonly Serilog.ILogger _logger;
    private readonly ChessGamesDbContext _context;

    public GamesService(Serilog.ILogger logger, ChessGamesDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    // On récupère la date de la dernière partie pour savoir quels mois on doit récupérer sur Chess.com
    public async Task<DateTime> GetLastGameDateAndTimeAsync(string playerUsername)
    {
        try
        {
            // On interroge la bdd
            var games = await _context.Games.Where(g => g.PlayerUsername == playerUsername).ToListAsync();

            // si bdd vide on renvoie une date par défaut
            if (!games.Any())
            {
                return new DateTime(1970, 1, 1, 0, 0, 0);
            }

            // on trie
            var lastGame = games.OrderByDescending(g => g.DateAndEndTime).FirstOrDefault();

            // si bdd vide on renvoie une date par défaut
            var result = lastGame?.DateAndEndTime ?? new DateTime(1970, 1, 1, 0, 0, 0);

            _logger.Information("{MethodName} - Last game date and time: {Result}", nameof(GetLastGameDateAndTimeAsync), result.ToString());
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("{MethodName} - {Exception}", nameof(GetLastGameDateAndTimeAsync), ex);
            throw new Exception();
        }
    }

    // On recupere les donnee de l'API Chess.com
    public async Task<List<string>> GetGamesFromChessComAsync(string username, DateTime lastGameDateAndTime, int maximumNumberOfMonthsToFetch)
    {
        var dataList = new List<string>();
        var now = DateTime.Now;
        var numberOfMonthsToFetch = maximumNumberOfMonthsToFetch;

        // on calcule le nombre d'appels API a faire a chess.com en fonction de la date de la derniere partie en bdd, et du nombre de mois qu'on veut récupérer, 
        // un mois = un appel API
        if (lastGameDateAndTime != DateTime.MinValue)
        {
            var lastGameYearMonth = new DateTime(lastGameDateAndTime.Year, lastGameDateAndTime.Month, 1);
            var monthsDifference = ((now.Year - lastGameYearMonth.Year) * 12) + now.Month - lastGameYearMonth.Month;
            numberOfMonthsToFetch = Math.Min(monthsDifference + 1, maximumNumberOfMonthsToFetch);
        }

        using (var httpClient = new HttpClient())
        {
            // En-tête User-Agent pour éviter le 403 Forbidden
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; ChessResultsStatsApp/1.0)");

            for (int i = numberOfMonthsToFetch - 1; i >= 0; i--)
            {
                var monthToFetch = now.AddMonths(-i);
                var url = $"https://api.chess.com/pub/player/{username}/games/{monthToFetch.Year}/{monthToFetch.Month:D2}";
                try
                {
                    var response = await httpClient.GetStringAsync(url);
                    dataList.Add(response);
                }
                catch (Exception e)
                {
                    _logger.Error("Error in {MethodName}", nameof(GetGamesFromChessComAsync), e);
                }
                _logger.Information("{MethodName} - API call - monthToFetch = {monthToFetch} - url = {url}", nameof(GetGamesFromChessComAsync), monthToFetch.ToString(), url);
            }
        }
        return dataList;
    }

    // On formatte les données
    public List<Game> CreateFormattedGamesList(List<string> dataList, string username, DateTime lastGameDateAndTime)
    {
        var gamesToReturn = new List<Game>();

        foreach (var data in dataList)
        {
            var obj = JObject.Parse(data);
            var gamesArray = obj["games"] as JArray;

            foreach (var gameJson in gamesArray)
            {
                var gamePgn = gameJson["pgn"].ToString();
                var white = gameJson["white"] as JObject;

                double accuracy = 0;
                if (gameJson["accuracies"] != null)
                {
                    accuracy = white["username"].ToString() == username
                        ? (double)gameJson["accuracies"]["white"]
                        : (double)gameJson["accuracies"]["black"];
                }

                using (var reader = new StringReader(gamePgn))
                {
                    string line;
                    Game currentGame = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("[Event "))
                        {
                            if (currentGame != null && currentGame.DateAndEndTime > lastGameDateAndTime)
                            {
                                gamesToReturn.Add(currentGame);
                            }
                            currentGame = new Game();
                        }

                        if (accuracy != 0 && currentGame != null && currentGame.Accuracy == 0)
                        {
                            currentGame.Accuracy = accuracy;
                        }

                        if (currentGame != null && line.StartsWith("["))
                        {
                            var key = line.Substring(1, line.IndexOf(' ') - 1);
                            var value = line.Substring(line.IndexOf('"') + 1, line.LastIndexOf('"') - line.IndexOf('"') - 1);

                            switch (key)
                            {
                                case "Event":
                                    currentGame.Event = value;
                                    break;
                                case "Site":
                                    currentGame.Site = value;
                                    break;
                                case "Date":
                                    currentGame.Date = DateTime.ParseExact(value, "yyyy.MM.dd", CultureInfo.InvariantCulture);
                                    break;
                                case "Round":
                                    currentGame.Round = value;
                                    break;
                                case "White":
                                    currentGame.White = value;
                                    break;
                                case "Black":
                                    currentGame.Black = value;
                                    break;
                                case "Result":
                                    currentGame.Result = value;
                                    break;
                                case "WhiteElo":
                                    currentGame.WhiteElo = int.Parse(value);
                                    break;
                                case "BlackElo":
                                    currentGame.BlackElo = int.Parse(value);
                                    break;
                                case "TimeControl":
                                    currentGame.TimeControl = value;
                                    break;
                                case "EndTime":
                                    currentGame.EndTime = TimeSpan.Parse(value);
                                    if (currentGame.Date != DateTime.MinValue)
                                    {
                                        currentGame.DateAndEndTime = currentGame.Date.Add(currentGame.EndTime);
                                    }
                                    break;
                                case "Termination":
                                    currentGame.Termination = value;
                                    break;
                                case "ECO":
                                    currentGame.Eco = value;
                                    break;
                                case "ECOUrl":
                                    var parts = value.Split('/');
                                    currentGame.Opening = parts.Last();
                                    break;
                            }
                        }
                        else if (currentGame != null && !string.IsNullOrWhiteSpace(line))
                        {
                            currentGame.Moves = (currentGame.Moves ?? "") + line + " ";
                        }
                    }

                    if (currentGame != null)
                    {
                        currentGame.PlayerElo = username == currentGame.White ? currentGame.WhiteElo : currentGame.BlackElo;
                        currentGame.PlayerUsername = username;
                        currentGame.Moves = FormatMoves(currentGame.Moves);
                        currentGame.Category = SetCategoryFromTimeControl(currentGame.TimeControl);
                        currentGame.ResultForPlayer = FindResultForPlayer(currentGame.Termination, currentGame.PlayerUsername);
                        currentGame.EndOfGameBy = HowEndedTheGame(currentGame.Termination);

                        if (currentGame.DateAndEndTime > lastGameDateAndTime)
                        {
                            gamesToReturn.Add(currentGame);
                        }
                    }
                }
            }
        }
        _logger.Information("{MethodName} - Number of games returned {gamesToReturn}", nameof(CreateFormattedGamesList), gamesToReturn.Count());
        return gamesToReturn;
    }

    // Fonction pour formatted les Moves utilisée par CreateFormattedGamesList
    public static string FormatMoves(string moves)
    {
        if (moves == null)
        {
            return string.Empty;
        }
        string cleanedString = Regex.Replace(moves, "\\{[^}]+\\}", "");
        var movesArray = cleanedString.Split(" ");
        var filteredMoves = movesArray.Where(move => !move.Contains("...")).ToArray();
        return string.Join(" ", filteredMoves).Replace("  ", " ");
    }

    // Fonction pour définir le resultat pour le joueur demandé, utilisée par CreateFormattedGamesList
    public static string FindResultForPlayer(string termination, string playerUsername)
    {
        if (termination.Contains("Partie nulle") || termination.Contains("drawn"))
        {
            return "drawn";
        }
        else if (termination.Contains(playerUsername))
        {
            return "won";
        }
        else
        {
            return "lost";
        }
    }

    // Fonction pour définir la cadence, utilisée par CreateFormattedGamesList
    public static string SetCategoryFromTimeControl(string timeControl)
    {
        return timeControl switch
        {
            "60" or "120" or "120+1" => "bullet",
            "180" or "180+2" or "300" => "blitz",
            "600" or "600+5" or "1800" => "rapid",
            _ => ""
        };
    }

    // Fonction pour définir comment s'est terminée la partie, utilisée par CreateFormattedGamesList
    public static string HowEndedTheGame(string termination)
    {
        if (termination.Contains("temps") || termination.Contains("time"))
        {
            return "time";
        }
        if (termination.Contains("échec et mat") || termination.Contains("checkmate"))
        {
            return "checkmate";
        }
        if (termination.Contains("abandon") || termination.Contains("resignation"))
        {
            return "abandonment";
        }
        if (termination.Contains("accord mutuel") || termination.Contains("mutual agreement"))
        {
            return "agreement";
        }
        if (termination.Contains("manque de matériel") || termination.Contains("insufficient material"))
        {
            return "lack of equipment";
        }
        if (termination.Contains("pat") || termination.Contains("stalemate"))
        {
            return "pat";
        }
        if (termination.Contains("répétition") || termination.Contains("repetition"))
        {
            return "repeat";
        }
        return "";
    }

    // Fonction pour sauvegarder en bdd
    public async Task SaveGameInDatabaseAsync(List<Game> games)
    {
        try
        {
            await _context.Games.AddRangeAsync(games);
            await _context.SaveChangesAsync();
            _logger.Information("{MethodName} - Number of games saved in database {games}", nameof(CreateFormattedGamesList), games.Count());
        }
        catch (Exception e)
        {
            _logger.Error("Error in {MethodName}", nameof(SaveGameInDatabaseAsync), e);
        }
    }

    // Fonction pour envoyer le contenu de la bdd au front
    public async Task<List<Game>> GetGamesAsync(string username)
    {
        List<Game> result = new List<Game>();
        try
        {
            result = await _context.Games.Where(g => g.PlayerUsername == username).ToListAsync();
        }
        catch (Exception e)
        {
            _logger.Error("Error in {MethodName}", nameof(GetGamesAsync), e);
        }
        return result;
    }
}

