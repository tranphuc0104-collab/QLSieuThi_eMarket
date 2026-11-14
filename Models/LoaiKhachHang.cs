using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class LoaiKhachHang
{
    [Key]
    [StringLength(50)]
    public string MaLoai { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string TenLoai { get; set; } = string.Empty;
    [StringLength(255)]
    public string? MoTa { get; set; }
}
