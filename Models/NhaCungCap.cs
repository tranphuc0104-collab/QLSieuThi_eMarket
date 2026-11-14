using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class NhaCungCap
{
    [Key]
    [StringLength(50)]
    public string MaNCC { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string TenNCC { get; set; } = string.Empty;
    [StringLength(255)]
    public string? DiaChi { get; set; }
    [StringLength(20)]
    public string? SoDienThoai { get; set; }
    [StringLength(100)]
    public string? Email { get; set; }
}
