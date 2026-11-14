using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class PhieuNhap
{
    [Key]
    [StringLength(50)]
    public string MaPhieuNhap { get; set; } = string.Empty;
    [StringLength(50)]
    public string? MaNCC { get; set; }
    [StringLength(50)]
    public string? MaKho { get; set; }
    [StringLength(50)]
    public string? MaNV { get; set; }
    public DateTime? NgayNhap { get; set; }
    public decimal TongTien { get; set; }

    public string? TenNCC { get; set; }
    public string? TenKho { get; set; }
    public List<ChiTietPhieuNhap> ChiTiet { get; set; } = new();
}
