using System.ComponentModel.DataAnnotations;

namespace ChessResultsStats_CSharp.Model;

public class WinratesByHour
{
    [MaxLength(255)]
    public string PlayerUsername { get; set; }

    [Required]
    public double Hour_0 { get; set; } = 0;

    [Required]
    public double Hour_1 { get; set; } = 0;

    [Required]
    public double Hour_2 { get; set; } = 0;

    [Required]
    public double Hour_3 { get; set; } = 0;

    [Required]
    public double Hour_4 { get; set; } = 0;

    [Required]
    public double Hour_5 { get; set; } = 0;

    [Required]
    public double Hour_6 { get; set; } = 0;

    [Required]
    public double Hour_7 { get; set; } = 0;

    [Required]
    public double Hour_8 { get; set; } = 0;

    [Required]
    public double Hour_9 { get; set; } = 0;

    [Required]
    public double Hour_10 { get; set; } = 0;

    [Required]
    public double Hour_11 { get; set; } = 0;

    [Required]
    public double Hour_12 { get; set; } = 0;

    [Required]
    public double Hour_13 { get; set; } = 0;

    [Required]
    public double Hour_14 { get; set; } = 0;

    [Required]
    public double Hour_15 { get; set; } = 0;

    [Required]
    public double Hour_16 { get; set; } = 0;

    [Required]
    public double Hour_17 { get; set; } = 0;

    [Required]
    public double Hour_18 { get; set; } = 0;

    [Required]
    public double Hour_19 { get; set; } = 0;

    [Required]
    public double Hour_20 { get; set; } = 0;

    [Required]
    public double Hour_21 { get; set; } = 0;

    [Required]
    public double Hour_22 { get; set; } = 0;

    [Required]
    public double Hour_23 { get; set; } = 0;
}
