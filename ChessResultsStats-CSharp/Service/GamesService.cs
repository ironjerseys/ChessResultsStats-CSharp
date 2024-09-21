//using MongoDB.Driver;
//using ChessResultsStats_CSharp.Model;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//namespace ChessResultsStats_CSharp.Service
//{
//    public class GamesService
//    {
//        private readonly IMongoCollection<Game> _games;

//        public GamesService(IMongoDatabase database)
//        {
//            _games = database.GetCollection<Game>("games");
//        }

//        // Méthode pour récupérer tous les jeux
//        public async Task<List<Game>> GetAllAsync() =>
//            await _games.Find(games => true).ToListAsync();

//        // Méthode pour récupérer les jeux par playerUsername
//        public async Task<List<Game>> GetGamesByPlayerUsernameAsync(string playerUsername) =>
//            await _games.Find(game => game.PlayerUsername == playerUsername).ToListAsync();
//    }
//}
