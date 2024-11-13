namespace ChessResultsStats_CSharp.Model;

public class Metadata
{
    public int Id { get; set; }
    public DateTime GameDate { get; set; }
    public string PlayerUsername { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int TotalMoves { get; set; }
    public string Opening { get; set; }
    public double Accuracy { get; set; }
    public string ResultForPlayer { get; set; }
}
