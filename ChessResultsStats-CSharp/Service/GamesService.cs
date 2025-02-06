using ChessResultsStats_CSharp.Model;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChessResultsStats_CSharp.Service
{
    public class GamesService
    {
        private readonly Serilog.ILogger _logger;

        // Stockage en mémoire :
        // Key   = nom d'utilisateur
        // Value = liste des parties déjà récupérées depuis l'API
        private static readonly Dictionary<string, List<Game>> _inMemoryGames = new();

        public GamesService(Serilog.ILogger logger)
        {
            _logger = logger;
        }

        // 1. On détermine la date de la dernière partie stockée en mémoire pour l'utilisateur
        //    (au lieu de la base de données).
        public DateTime GetLastGameDateAndTime(string playerUsername)
        {
            if (!_inMemoryGames.ContainsKey(playerUsername) || !_inMemoryGames[playerUsername].Any())
            {
                // Rien en mémoire => On force une date de départ très ancienne
                return new DateTime(1970, 1, 1, 0, 0, 0);
            }

            // Sinon on retourne la plus récente
            var lastGame = _inMemoryGames[playerUsername]
                .OrderByDescending(g => g.DateAndEndTime)
                .First();
            return lastGame.DateAndEndTime;
        }

        // 2. Récupération des données depuis l’API Chess.com
        public async Task<List<string>> GetGamesFromChessComAsync(
            string username,
            DateTime lastGameDateAndTime,
            int maximumNumberOfMonthsToFetch)
        {
            var dataList = new List<string>();
            var now = DateTime.Now;
            var numberOfMonthsToFetch = maximumNumberOfMonthsToFetch;

            // Calcul du nombre de mois à récupérer
            if (lastGameDateAndTime != DateTime.MinValue)
            {
                var lastGameYearMonth = new DateTime(lastGameDateAndTime.Year, lastGameDateAndTime.Month, 1);
                var monthsDifference = ((now.Year - lastGameYearMonth.Year) * 12)
                                       + now.Month - lastGameYearMonth.Month;
                numberOfMonthsToFetch = Math.Min(monthsDifference + 1, maximumNumberOfMonthsToFetch);
            }

            using var httpClient = new HttpClient();
            // En-tête User-Agent pour éviter le 403 Forbidden
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (compatible; ChessResultsStatsApp/1.0)");

            for (int i = numberOfMonthsToFetch - 1; i >= 0; i--)
            {
                var monthToFetch = now.AddMonths(-i);
                var url = $"https://api.chess.com/pub/player/{username}/games/" +
                          $"{monthToFetch.Year}/{monthToFetch.Month:D2}";

                try
                {
                    var response = await httpClient.GetStringAsync(url);
                    dataList.Add(response);
                    _logger.Information(
                        "{MethodName} - Fetched month {month} from {url}",
                        nameof(GetGamesFromChessComAsync),
                        monthToFetch.ToString("yyyy-MM"),
                        url);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in {MethodName}", nameof(GetGamesFromChessComAsync));
                }
            }
            return dataList;
        }

        // 3. On formate les données brutes en objets `Game`
        public List<Game> CreateFormattedGamesList(
            List<string> dataList,
            string username,
            DateTime lastGameDateAndTime)
        {
            var gamesToReturn = new List<Game>();

            foreach (var data in dataList)
            {
                var obj = JObject.Parse(data);
                var gamesArray = obj["games"] as JArray;
                if (gamesArray == null) continue;

                foreach (var gameJson in gamesArray)
                {
                    var gamePgn = gameJson["pgn"]?.ToString() ?? "";
                    var white = gameJson["white"] as JObject;
                    double accuracy = 0;

                    // Si l'objet "accuracies" existe dans le JSON
                    if (gameJson["accuracies"] != null && white != null)
                    {
                        var isWhitePlayer = (white["username"]?.ToString() ?? "") == username;
                        accuracy = isWhitePlayer
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
                                // Dès qu'on detecte un nouvel Event,
                                // on ajoute le précédent s'il est plus récent que lastGameDateAndTime
                                if (currentGame != null &&
                                    currentGame.DateAndEndTime > lastGameDateAndTime)
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
                                var value = line.Substring(
                                    line.IndexOf('"') + 1,
                                    line.LastIndexOf('"') - line.IndexOf('"') - 1);

                                switch (key)
                                {
                                    case "Event":
                                        currentGame.Event = value;
                                        break;
                                    case "Site":
                                        currentGame.Site = value;
                                        break;
                                    case "Date":
                                        currentGame.Date = DateTime.ParseExact(
                                            value, "yyyy.MM.dd", CultureInfo.InvariantCulture);
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
                            currentGame.PlayerElo =
                                (username == currentGame.White) ? currentGame.WhiteElo : currentGame.BlackElo;
                            currentGame.PlayerUsername = username;
                            currentGame.Moves = FormatMoves(currentGame.Moves);
                            currentGame.Category = SetCategoryFromTimeControl(currentGame.TimeControl);
                            currentGame.ResultForPlayer =
                                FindResultForPlayer(currentGame.Termination, currentGame.PlayerUsername);
                            currentGame.EndOfGameBy = HowEndedTheGame(currentGame.Termination);

                            if (currentGame.DateAndEndTime > lastGameDateAndTime)
                            {
                                gamesToReturn.Add(currentGame);
                            }
                        }
                    }
                }
            }

            _logger.Information(
                "{MethodName} - Number of new games returned = {Count}",
                nameof(CreateFormattedGamesList),
                gamesToReturn.Count);

            return gamesToReturn;
        }

        // 4. Ici, au lieu de "sauver en BDD", on fusionne simplement en mémoire.
        public Task SaveGameInDatabaseAsync(List<Game> games)
        {
            // Si aucune partie => on ne fait rien
            if (games == null || !games.Any()) return Task.CompletedTask;

            // On suppose que toutes les parties viennent du même username
            var username = games.First().PlayerUsername;
            if (!_inMemoryGames.ContainsKey(username))
            {
                _inMemoryGames[username] = new List<Game>();
            }

            // On ajoute uniquement celles qui ne sont pas déjà présentes
            // (ou on peut, plus simplement, tout ajouter si on n'a pas peur des doublons).
            var existing = _inMemoryGames[username];
            foreach (var g in games)
            {
                // Vérifie si on n'a pas déjà la même date+heure
                if (!existing.Any(x => x.DateAndEndTime == g.DateAndEndTime && x.PlayerUsername == username))
                {
                    existing.Add(g);
                }
            }

            _logger.Information(
                "{MethodName} - {Count} games saved in memory for user {User}. Total in memory = {Total}",
                nameof(SaveGameInDatabaseAsync),
                games.Count,
                username,
                existing.Count);

            return Task.CompletedTask;
        }

        // 5. Retourner toutes les parties en mémoire pour un username
        public List<Game> GetGames(string username)
        {
            if (!_inMemoryGames.ContainsKey(username))
            {
                return new List<Game>();
            }
            return _inMemoryGames[username];
        }

        public static string FormatMoves(string moves)
        {
            if (moves == null) return string.Empty;
            string cleanedString = Regex.Replace(moves, "\\{[^}]+\\}", "");
            var movesArray = cleanedString.Split(" ");
            var filteredMoves = movesArray.Where(move => !move.Contains("...")).ToArray();
            return string.Join(" ", filteredMoves).Replace("  ", " ");
        }

        public static string FindResultForPlayer(string termination, string playerUsername)
        {
            if (termination.Contains("Partie nulle", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("drawn", StringComparison.OrdinalIgnoreCase))
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

        public static string HowEndedTheGame(string termination)
        {
            if (termination.Contains("temps", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("time", StringComparison.OrdinalIgnoreCase))
                return "time";
            if (termination.Contains("échec et mat", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("checkmate", StringComparison.OrdinalIgnoreCase))
                return "checkmate";
            if (termination.Contains("abandon", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("resignation", StringComparison.OrdinalIgnoreCase))
                return "abandonment";
            if (termination.Contains("accord mutuel", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("mutual agreement", StringComparison.OrdinalIgnoreCase))
                return "agreement";
            if (termination.Contains("manque de matériel", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("insufficient material", StringComparison.OrdinalIgnoreCase))
                return "lack of equipment";
            if (termination.Contains("pat", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("stalemate", StringComparison.OrdinalIgnoreCase))
                return "pat";
            if (termination.Contains("répétition", StringComparison.OrdinalIgnoreCase)
                || termination.Contains("repetition", StringComparison.OrdinalIgnoreCase))
                return "repeat";

            return "";
        }

        public Task<WinratesByHour> GetWinratesByHourAsync(string playerUsername)
        {
            // On calcule en direct à partir des parties en mémoire
            var games = GetGames(playerUsername);
            if (!games.Any())
            {
                // Retourner un objet vide ou null.
                return Task.FromResult<WinratesByHour>(null);
            }

            int[] gamesPlayed = new int[24];
            int[] gamesWon = new int[24];

            foreach (var game in games)
            {
                var hour = game.DateAndEndTime.Hour;
                gamesPlayed[hour]++;
                if (game.ResultForPlayer == "won")
                {
                    gamesWon[hour]++;
                }
            }

            // Calcul des ratios
            double[] winrates = new double[24];
            for (int i = 0; i < 24; i++)
            {
                winrates[i] = gamesPlayed[i] > 0
                    ? (double)gamesWon[i] / gamesPlayed[i]
                    : 0.0;
            }

            // On renvoie l'objet WinratesByHour directement, sans stockage BDD
            var result = new WinratesByHour
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
                Hour_23 = winrates[23]
            };

            return Task.FromResult(result);
        }

        public Task<AverageMovesByPiece> UpdateAverageMovesByPieceAsync(string playerUsername)
        {
            // On récupère toutes les parties en mémoire
            var games = GetGames(playerUsername);
            if (!games.Any())
            {
                // Soit on renvoie null ou on lève une exception
                return Task.FromResult<AverageMovesByPiece>(null);
            }

            int totalGames = games.Count;

            int pawnMoves = 0, knightMoves = 0, bishopMoves = 0;
            int rookMoves = 0, queenMoves = 0, kingMoves = 0;

            // On compte les coups pour chaque partie
            foreach (var game in games)
            {
                var movesDict = CountPieceMoves(game.Moves);
                pawnMoves += movesDict["pawn"];
                knightMoves += movesDict["knight"];
                bishopMoves += movesDict["bishop"];
                rookMoves += movesDict["rook"];
                queenMoves += movesDict["queen"];
                kingMoves += movesDict["king"];
            }

            // On calcule les moyennes
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

            // On renvoie simplement l'objet (pas de stockage BDD).
            return Task.FromResult(averageMoves);
        }

        private Dictionary<string, int> CountPieceMoves(string movesString)
        {
            var movesCount = new Dictionary<string, int>
            {
                { "pawn",   0 },
                { "knight", 0 },
                { "bishop", 0 },
                { "rook",   0 },
                { "queen",  0 },
                { "king",   0 }
            };

            if (string.IsNullOrWhiteSpace(movesString))
                return movesCount;

            var moves = movesString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var move in moves)
            {
                // Ignorer les numéros de tour "1.", "2." etc.
                if (char.IsDigit(move[0]) && move.Contains('.'))
                    continue;

                // On se base sur la notation classique
                if (move.StartsWith("N")) movesCount["knight"]++;
                else if (move.StartsWith("B")) movesCount["bishop"]++;
                else if (move.StartsWith("R")) movesCount["rook"]++;
                else if (move.StartsWith("Q")) movesCount["queen"]++;
                else if (move.StartsWith("K")) movesCount["king"]++;
                else if (char.IsLower(move[0]))
                    movesCount["pawn"]++;
            }

            return movesCount;
        }
    }
}
