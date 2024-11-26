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

                        if (accuracy != 0 && currentGame != null && (currentGame.Accuracy ?? 0) == 0)
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
        else if (termination.Contains(playerUsername, StringComparison.OrdinalIgnoreCase))
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

    public async Task<WinratesByHour> GetWinratesByHourAsync(string playerUsername)
    {
        // Récupérer les parties du joueur
        var games = await _context.Games
            .Where(g => g.PlayerUsername == playerUsername)
            .ToListAsync();

        if (!games.Any())
        {
            return null; // Aucun jeu à traiter
        }

        // Initialiser un tableau pour stocker les parties jouées et gagnées par heure
        int[] gamesPlayed = new int[24];
        int[] gamesWon = new int[24];

        // Parcourir les parties pour calculer les données par heure
        foreach (var game in games)
        {
            var hour = game.DateAndEndTime.Hour;
            gamesPlayed[hour]++;

            if (game.ResultForPlayer == "won")
            {
                gamesWon[hour]++;
            }
        }

        // Calculer les winrates
        var winrates = new double[24];
        for (int i = 0; i < 24; i++)
        {
            winrates[i] = gamesPlayed[i] > 0 ? (double)gamesWon[i] / gamesPlayed[i] : 0;
        }

        // Vérifier si une entrée pour ce joueur existe déjà dans WinratesByHour
        var existingEntry = await _context.WinratesByHour.FindAsync(playerUsername);

        if (existingEntry != null)
        {
            // Mettre à jour les colonnes existantes
            existingEntry.Hour_0 = winrates[0];
            existingEntry.Hour_1 = winrates[1];
            existingEntry.Hour_2 = winrates[2];
            existingEntry.Hour_3 = winrates[3];
            existingEntry.Hour_4 = winrates[4];
            existingEntry.Hour_5 = winrates[5];
            existingEntry.Hour_6 = winrates[6];
            existingEntry.Hour_7 = winrates[7];
            existingEntry.Hour_8 = winrates[8];
            existingEntry.Hour_9 = winrates[9];
            existingEntry.Hour_10 = winrates[10];
            existingEntry.Hour_11 = winrates[11];
            existingEntry.Hour_12 = winrates[12];
            existingEntry.Hour_13 = winrates[13];
            existingEntry.Hour_14 = winrates[14];
            existingEntry.Hour_15 = winrates[15];
            existingEntry.Hour_16 = winrates[16];
            existingEntry.Hour_17 = winrates[17];
            existingEntry.Hour_18 = winrates[18];
            existingEntry.Hour_19 = winrates[19];
            existingEntry.Hour_20 = winrates[20];
            existingEntry.Hour_21 = winrates[21];
            existingEntry.Hour_22 = winrates[22];
            existingEntry.Hour_23 = winrates[23];

            _context.WinratesByHour.Update(existingEntry);
            await _context.SaveChangesAsync();
            return existingEntry;
        }
        else
        {
            // Ajouter une nouvelle entrée si elle n'existe pas
            var newEntry = new WinratesByHour
            {
                PlayerUsername = playerUsername,
                Hour_0 = winrates[0],
                Hour_1 = winrates[1],
                Hour_2 = winrates[2],
                Hour_3 = winrates[3],
                Hour_4 = winrates[4],
                Hour_5 = winrates[5],
                Hour_6 = winrates[6],
                Hour_7 = winrates[7],
                Hour_8 = winrates[8],
                Hour_9 = winrates[9],
                Hour_10 = winrates[10],
                Hour_11 = winrates[11],
                Hour_12 = winrates[12],
                Hour_13 = winrates[13],
                Hour_14 = winrates[14],
                Hour_15 = winrates[15],
                Hour_16 = winrates[16],
                Hour_17 = winrates[17],
                Hour_18 = winrates[18],
                Hour_19 = winrates[19],
                Hour_20 = winrates[20],
                Hour_21 = winrates[21],
                Hour_22 = winrates[22],
                Hour_23 = winrates[23],
            };

            await _context.WinratesByHour.AddAsync(newEntry);
            await _context.SaveChangesAsync();
            return newEntry;
        }
    }

    public async Task<AverageMovesByPiece> UpdateAverageMovesByPieceAsync(string playerUsername)
    {
        // Récupérer toutes les parties du joueur
        var games = await _context.Games.Where(g => g.PlayerUsername == playerUsername).ToListAsync();

        if (!games.Any())
        {
            throw new Exception("AverageMovesByPiece table is not configured in DbContext.");
        }

        // Initialiser les compteurs
        int totalGames = games.Count;
        int pawnMoves = 0, knightMoves = 0, bishopMoves = 0;
        int rookMoves = 0, queenMoves = 0, kingMoves = 0;

        // Parcourir les parties et compter les coups par pièce
        foreach (var game in games)
        {
            var moves = CountPieceMoves(game.Moves);

            pawnMoves += moves["pawn"];
            knightMoves += moves["knight"];
            bishopMoves += moves["bishop"];
            rookMoves += moves["rook"];
            queenMoves += moves["queen"];
            kingMoves += moves["king"];
        }

        // Calculer les moyennes
        var averageMoves = new AverageMovesByPiece
        {
            PlayerUsername = playerUsername,
            AvgPawnMoves = (double)pawnMoves / totalGames,
            AvgKnightMoves = (double)knightMoves / totalGames,
            AvgBishopMoves = (double)bishopMoves / totalGames,
            AvgRookMoves = (double)rookMoves / totalGames,
            AvgQueenMoves = (double)queenMoves / totalGames,
            AvgKingMoves = (double)kingMoves / totalGames
        };

        // Vérifier si une entrée existe déjà
        var existingEntry = await _context.AverageMovesByPiece.FindAsync(playerUsername);

        if (existingEntry != null)
        {
            // Mettre à jour l'entrée existante
            existingEntry.AvgPawnMoves = averageMoves.AvgPawnMoves;
            existingEntry.AvgKnightMoves = averageMoves.AvgKnightMoves;
            existingEntry.AvgBishopMoves = averageMoves.AvgBishopMoves;
            existingEntry.AvgRookMoves = averageMoves.AvgRookMoves;
            existingEntry.AvgQueenMoves = averageMoves.AvgQueenMoves;
            existingEntry.AvgKingMoves = averageMoves.AvgKingMoves;

            _context.AverageMovesByPiece.Update(existingEntry);
        }
        else
        {
            // Ajouter une nouvelle entrée
            await _context.AverageMovesByPiece.AddAsync(averageMoves);
        }

        // Sauvegarder les modifications
        await _context.SaveChangesAsync();
        return averageMoves;
    }

    // Compter les coups par type de pièce
    private Dictionary<string, int> CountPieceMoves(string movesString)
    {
        // Initialiser les compteurs
        var movesCount = new Dictionary<string, int>
    {
        { "pawn", 0 },
        { "knight", 0 },
        { "bishop", 0 },
        { "rook", 0 },
        { "queen", 0 },
        { "king", 0 }
    };

        // Diviser les coups en mouvements individuels
        var moves = movesString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var move in moves)
        {
            // Ignorer les numéros de tour (par exemple, "1.", "2.", etc.)
            if (char.IsDigit(move[0]) && move.Contains('.'))
            {
                continue;
            }

            // Compter les coups en fonction du premier caractère
            if (move.StartsWith("N")) movesCount["knight"]++; // Cavalier
            else if (move.StartsWith("B")) movesCount["bishop"]++; // Fou
            else if (move.StartsWith("R")) movesCount["rook"]++; // Tour
            else if (move.StartsWith("Q")) movesCount["queen"]++; // Dame
            else if (move.StartsWith("K")) movesCount["king"]++; // Roi
            else if (char.IsLower(move[0])) movesCount["pawn"]++; // Pion (aucun préfixe)
        }

        return movesCount;
    }


}

