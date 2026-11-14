using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class NhanVien
{
    [Key]
    [StringLength(50)]
    public string MaNV { get; set; } = string.Empty;
    [Required]
    [StringLength(100)]
    public string HoTen { get; set; } = string.Empty;
    [StringLength(20)]
    public string? SoDienThoai { get; set; }
    [StringLength(100)]
    public string? Email { get; set; }
    [StringLength(50)]
    public string? TenDangNhap { get; set; }
    [StringLength(100)]
    public string? MatKhau { get; set; }
    [StringLength(50)]
    public string? VaiTro { get; set; }
}
