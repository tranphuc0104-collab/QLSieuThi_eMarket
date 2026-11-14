using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class CongNo
{
    [Key]
    [StringLength(50)]
    public string MaCongNo { get; set; } = string.Empty;
    [StringLength(50)]
    public string? MaNV { get; set; }
    [StringLength(50)]
    public string? MaKhachHang { get; set; }
    [StringLength(50)]
    public string? MaNCC { get; set; }
    [StringLength(50)]
    public string? MaDonHang { get; set; }
    [StringLength(50)]
    public string? LoaiCongNo { get; set; } // "Phải thu" (KH) hoặc "Phải trả" (NCC)
    public DateTime? NgayPhatSinh { get; set; }
    public decimal SoTienNo { get; set; }
    public DateTime? HanThanhToan { get; set; }
    [StringLength(50)]
    public string? TrangThai { get; set; }

    public string? DoiTuongTen { get; set; } // Tên khách hàng hoặc tên NCC
    public string? CanhBao { get; set; } // "Quá hạn", "Vượt mức", "Bình thường"
}
