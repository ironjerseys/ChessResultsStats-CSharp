﻿
//using MongoDB.Driver;
//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Net.Http;
//using System.Threading.Tasks;
//using System.Text.RegularExpressions;
//using Microsoft.Extensions.Logging;
//using ChessResultsStats_CSharp.Model;
//using static System.Runtime.InteropServices.JavaScript.JSType;

//namespace ChessResultsStats_CSharp.Service;

//public class GamesService
//{
//    private readonly ILogger<GamesService> _logger;

//    public GamesService(ILogger<GamesService> logger)
//    {
//        _logger = logger;
//    }

//    public async Task<DateTime> GetLastGameDateAndTimeAsync(string username)
//    {
//        var games = await _gamesRepository.FindByPlayerUsernameAsync(username);

//        if (!games.Any())
//        {
//            return new DateTime(1970, 1, 1, 0, 0, 0);
//        }

//        var lastGame = games.OrderByDescending(g => g.DateAndEndTime).FirstOrDefault();

//        return lastGame?.DateAndEndTime ?? new DateTime(1970, 1, 1, 0, 0, 0);
//    }

//    public async Task<List<string>> GetGamesFromChessComAsync(string username, DateTime lastGameDateAndTime, int maximumNumberOfMonthsToFetch)
//    {
//        var dataList = new List<string>();
//        var now = DateTime.Now;
//        var numberOfMonthsToFetch = maximumNumberOfMonthsToFetch;

//        if (lastGameDateAndTime != DateTime.MinValue)
//        {
//            var lastGameYearMonth = new DateTime(lastGameDateAndTime.Year, lastGameDateAndTime.Month, 1);
//            var monthsDifference = ((now.Year - lastGameYearMonth.Year) * 12) + now.Month - lastGameYearMonth.Month;
//            numberOfMonthsToFetch = Math.Min(monthsDifference + 1, maximumNumberOfMonthsToFetch);
//        }

//        using (var httpClient = new HttpClient())
//        {
//            for (int i = numberOfMonthsToFetch - 1; i >= 0; i--)
//            {
//                var monthToFetch = now.AddMonths(-i);
//                var url = $"https://api.chess.com/pub/player/{username}/games/{monthToFetch.Year}/{monthToFetch.Month:D2}";
//                try
//                {
//                    var response = await httpClient.GetStringAsync(url);
//                    dataList.Add(response);
//                }
//                catch (Exception e)
//                {
//                    _logger.LogError("Error in GetGamesFromChessComAsync", e);
//                }
//            }
//        }

//        return dataList;
//    }

//    public List<Game> CreateFormattedGamesList(List<string> dataList, string username, DateTime lastGameDateAndTime)
//    {
//        var gamesToReturn = new List<Game>();

//        foreach (var data in dataList)
//        {
//            var obj = JObject.Parse(data);
//            var gamesArray = obj["games"] as JArray;

//            foreach (var gameJson in gamesArray)
//            {
//                var gamePgn = gameJson["pgn"].ToString();
//                var white = gameJson["white"] as JObject;

//                double accuracy = 0;
//                if (gameJson["accuracies"] != null)
//                {
//                    accuracy = white["username"].ToString() == username
//                        ? (double)gameJson["accuracies"]["white"]
//                        : (double)gameJson["accuracies"]["black"];
//                }

//                using (var reader = new StringReader(gamePgn))
//                {
//                    string line;
//                    Game currentGame = null;

//                    while ((line = reader.ReadLine()) != null)
//                    {
//                        if (line.StartsWith("[Event "))
//                        {
//                            if (currentGame != null && currentGame.DateAndEndTime > lastGameDateAndTime)
//                            {
//                                gamesToReturn.Add(currentGame);
//                            }
//                            currentGame = new Game();
//                        }

//                        if (accuracy != 0 && currentGame != null && currentGame.Accuracy == 0)
//                        {
//                            currentGame.Accuracy = accuracy;
//                        }

//                        if (currentGame != null && line.StartsWith("["))
//                        {
//                            var key = line.Substring(1, line.IndexOf(' ') - 1);
//                            var value = line.Substring(line.IndexOf('"') + 1, line.LastIndexOf('"') - line.IndexOf('"') - 1);

//                            switch (key)
//                            {
//                                case "Event":
//                                    currentGame.Event = value;
//                                    break;
//                                case "Site":
//                                    currentGame.Site = value;
//                                    break;
//                                case "Date":
//                                    currentGame.Date = DateTime.ParseExact(value, "yyyy.MM.dd", null);
//                                    break;
//                                case "Round":
//                                    currentGame.Round = value;
//                                    break;
//                                case "White":
//                                    currentGame.White = value;
//                                    break;
//                                case "Black":
//                                    currentGame.Black = value;
//                                    break;
//                                case "Result":
//                                    currentGame.Result = value;
//                                    break;
//                                case "WhiteElo":
//                                    currentGame.WhiteElo = int.Parse(value);
//                                    break;
//                                case "BlackElo":
//                                    currentGame.BlackElo = int.Parse(value);
//                                    break;
//                                case "TimeControl":
//                                    currentGame.TimeControl = value;
//                                    break;
//                                case "EndTime":
//                                    currentGame.EndTime = TimeSpan.Parse(value);
//                                    if (currentGame.Date != DateTime.MinValue)
//                                    {
//                                        currentGame.DateAndEndTime = currentGame.Date.Add(currentGame.EndTime);
//                                    }
//                                    break;
//                                case "Termination":
//                                    currentGame.Termination = value;
//                                    break;
//                                case "ECO":
//                                    currentGame.Eco = value;
//                                    break;
//                                case "ECOUrl":
//                                    var parts = value.Split('/');
//                                    currentGame.Opening = parts.Last();
//                                    break;
//                            }
//                        }
//                        else if (currentGame != null && !string.IsNullOrWhiteSpace(line))
//                        {
//                            currentGame.Moves = (currentGame.Moves ?? "") + line + " ";
//                        }
//                    }

//                    if (currentGame != null)
//                    {
//                        currentGame.PlayerElo = username == currentGame.White ? currentGame.WhiteElo : currentGame.BlackElo;
//                        currentGame.PlayerUsername = username;
//                        currentGame.Moves = FormatMoves(currentGame.Moves);
//                        currentGame.Category = SetCategoryFromTimeControl(currentGame.TimeControl);
//                        currentGame.ResultForPlayer = FindResultForPlayer(currentGame.Termination, currentGame.PlayerUsername);
//                        currentGame.EndOfGameBy = HowEndedTheGame(currentGame.Termination);

//                        if (currentGame.DateAndEndTime > lastGameDateAndTime)
//                        {
//                            gamesToReturn.Add(currentGame);
//                        }
//                    }
//                }
//            }
//        }

//        return gamesToReturn;
//    }

//    public static string FormatMoves(string moves)
//    {
//        string cleanedString = Regex.Replace(moves, "\\{[^}]+\\}", "");
//        var movesArray = cleanedString.Split(" ");
//        var filteredMoves = movesArray.Where(move => !move.Contains("...")).ToArray();
//        return string.Join(" ", filteredMoves).Replace("  ", " ");
//    }

//    public static string FindResultForPlayer(string termination, string playerUsername)
//    {
//        if (termination.Contains("Partie nulle"))
//        {
//            return "drawn";
//        }
//        else if (termination.Contains(playerUsername))
//        {
//            return "won";
//        }
//        else
//        {
//            return "lost";
//        }
//    }

//    public static string SetCategoryFromTimeControl(string timeControl)
//    {
//        return timeControl switch
//        {
//            "60" or "120" or "120+1" => "bullet",
//            "180" or "180+2" or "300" => "blitz",
//            "600" or "600+5" or "1800" => "rapid",
//            _ => ""
//        };
//    }

//    public static string HowEndedTheGame(string termination)
//    {
//        if (termination.Contains("temps") || termination.Contains("time"))
//        {
//            return "time";
//        }
//        if (termination.Contains("échec et mat") || termination.Contains("checkmate"))
//        {
//            return "checkmate";
//        }
//        if (termination.Contains("abandon") || termination.Contains("resignation"))
//        {
//            return "abandonment";
//        }
//        if (termination.Contains("accord mutuel") || termination.Contains("mutual agreement"))
//        {
//            return "agreement";
//        }
//        if (termination.Contains("manque de matériel") || termination.Contains("insufficient material"))
//        {
//            return "lack of equipment";
//        }
//        if (termination.Contains("pat") || termination.Contains("stalemate"))
//        {
//            return "pat";
//        }
//        if (termination.Contains("répétition") || termination.Contains("repetition"))
//        {
//            return "repeat";
//        }
//        return "";
//    }

//    public async Task SaveGameInDatabaseAsync(List<Game> games)
//    {
//        try
//        {
//            await _gamesRepository.InsertManyAsync(games);
//        }
//        catch (Exception e)
//        {
//            _logger.LogError("Error in SaveGameInDatabaseAsync", e);
//        }
//    }

//    public async Task<List<Game>> GetGamesAsync(string username)
//    {
//        return await _gamesRepository.FindByPlayerUsernameAsync(username);
//    }
//}

