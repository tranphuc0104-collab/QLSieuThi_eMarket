using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class ChiTietDonHang
{
    [StringLength(50)]
    public string MaDonHang { get; set; } = string.Empty;
    [StringLength(50)]
    public string MaSP { get; set; } = string.Empty;
    public int SoLuong { get; set; }
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }

    public string? TenSP { get; set; }
    public string? DonViTinh { get; set; }
}
