using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class Voucher
{
    [Key]
    [StringLength(50)]
    public string MaVoucher { get; set; } = string.Empty;
    [StringLength(50)]
    public string MaKhachHang { get; set; } = string.Empty;
    public decimal GiaTri { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime NgayHetHan { get; set; }
    [StringLength(50)]
    public string? MaDonHangSuDung { get; set; }
}
