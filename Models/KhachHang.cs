using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class KhachHang
{
    [Key]
    [StringLength(50)]
    public string MaKhachHang { get; set; } = string.Empty;
    [StringLength(50)]
    public string? MaLoai { get; set; }
    [StringLength(50)]
    public string? MaNV { get; set; }
    [Required]
    [StringLength(100)]
    public string HoTen { get; set; } = string.Empty;
    [StringLength(20)]
    public string? SoDienThoai { get; set; }
    [StringLength(100)]
    public string? Email { get; set; }
    [StringLength(255)]
    public string? DiaChi { get; set; }

    // Navigation / helper properties
    public string? TenLoai { get; set; }
    public int DiemTichLuy { get; set; }
}
