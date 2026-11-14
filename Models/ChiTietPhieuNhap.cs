using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class ChiTietPhieuNhap
{
    [StringLength(50)]
    public string MaPhieuNhap { get; set; } = string.Empty;
    [StringLength(50)]
    public string MaSP { get; set; } = string.Empty;
    public int SoLuong { get; set; }
    public decimal DonGiaNhap { get; set; }
    public decimal ThanhTien { get; set; }

    public string? TenSP { get; set; }
}
