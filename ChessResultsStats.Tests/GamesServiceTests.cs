using ChessResultsStats_CSharp.Service;
using Moq;

namespace ChessResultsStats.Tests
{
    public class GamesServiceTests
    {
        [Fact]
        public void CreateFormattedGamesList_ShouldReturnExpectedGameObject()
        {
            // Arrange
            var mockLogger = new Mock<Serilog.ILogger>();

            // ?? On instancie GamesService sans DbContext,
            //    en supposant que votre nouveau constructeur
            //    n'en a plus besoin.
            var service = new GamesService(mockLogger.Object);

            // JSON compact correspondant aux données réelles
            var sampleData = new List<string>
            {
                "{\"games\":[{\"pgn\":\"[Event \\\"Live Chess\\\"]\\n[Site \\\"Chess.com\\\"]\\n[Date \\\"2024.11.16\\\"]\\n[Round \\\"-\\\"]\\n[White \\\"PokemonRwwn\\\"]\\n[Black \\\"JustMovingPieces01\\\"]\\n[Result \\\"1-0\\\"]\\n[WhiteElo \\\"972\\\"]\\n[BlackElo \\\"962\\\"]\\n[TimeControl \\\"180+2\\\"]\\n[EndTime \\\"09:41:17\\\"]\\n[Termination \\\"PokemonRwwn won by resignation\\\"]\\n[ECO \\\"B12\\\"]\\n[ECOUrl \\\"https://www.chess.com/openings/Caro-Kann-Defense-Fantasy-Variation\\\"]\",\"accuracies\":{\"white\":0,\"black\":0},\"white\":{\"username\":\"PokemonRwwn\"},\"black\":{\"username\":\"JustMovingPieces01\"}}]}"
            };

            var username = "JustMovingPieces01";
            var lastGameDateAndTime = new DateTime(2024, 11, 15); // Date avant la partie

            // Act
            var result = service.CreateFormattedGamesList(sampleData, username, lastGameDateAndTime);

            // Assert
            Assert.Single(result); // Vérifie qu'un seul jeu est retourné
            var game = result[0];

            // Vérifications basées sur l'exemple d'objet
            Assert.Equal("Live Chess", game.Event);
            Assert.Equal("Chess.com", game.Site);
            Assert.Equal(new DateTime(2024, 11, 16), game.Date);
            Assert.Equal("-", game.Round);
            Assert.Equal("PokemonRwwn", game.White);
            Assert.Equal("JustMovingPieces01", game.Black);
            Assert.Equal("1-0", game.Result);
            Assert.Equal(972, game.WhiteElo);
            Assert.Equal(962, game.BlackElo);
            Assert.Equal(962, game.PlayerElo); // Elo du joueur noir
            Assert.Equal("180+2", game.TimeControl);
            Assert.Equal("blitz", game.Category); // Déduit de TimeControl
            Assert.Equal(new TimeSpan(9, 41, 17), game.EndTime);
            Assert.Equal("PokemonRwwn won by resignation", game.Termination);
            Assert.NotNull(game.Moves); // Moves ne doit pas être null
            Assert.Equal("JustMovingPieces01", game.PlayerUsername);
            Assert.Equal("lost", game.ResultForPlayer); // Résultat pour le joueur noir
            Assert.Equal("abandonment", game.EndOfGameBy); // Fin de partie par abandon
            Assert.Null(game.Accuracy); // Aucune précision pour le joueur noir
            Assert.Equal("Caro-Kann-Defense-Fantasy-Variation", game.Opening);
            Assert.Equal("B12", game.Eco);
            Assert.Equal(new DateTime(2024, 11, 16, 9, 41, 17), game.DateAndEndTime);
        }
    }
}
