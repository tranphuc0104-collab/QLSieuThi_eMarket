using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class Kho
{
    [Key]
    [StringLength(50)]
    public string MaKho { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string TenKho { get; set; } = string.Empty;
    [StringLength(255)]
    public string? DiaChi { get; set; }
}
