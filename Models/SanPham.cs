using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class SanPham
{
    [Key]
    [StringLength(50)]
    public string MaSP { get; set; } = string.Empty;
    [StringLength(50)]
    public string? MaDanhMuc { get; set; }
    [Required]
    [StringLength(100)]
    public string TenSP { get; set; } = string.Empty;
    [StringLength(255)]
    public string? MoTa { get; set; }
    public decimal GiaBan { get; set; }
    public DateTime? HanSuDung { get; set; }
    [StringLength(50)]
    public string? DonViTinh { get; set; }
    [StringLength(500)]
    public string? HinhAnh { get; set; }

    // Helper
    public string? TenDanhMuc { get; set; }
    public int SoLuongTon { get; set; }
}
