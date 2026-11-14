using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

public class DonHang
{
    [Key]
    [StringLength(50)]
    public string MaDonHang { get; set; } = string.Empty;
    [StringLength(50)]
    public string? MaKhachHang { get; set; }
    [StringLength(50)]
    public string? MaNV { get; set; }
    public DateTime? NgayDat { get; set; }
    [StringLength(50)]
    public string? TrangThai { get; set; }
    public decimal TongTien { get; set; }

    // Navigation
    public string? TenKhachHang { get; set; }
    public string? SoDienThoai { get; set; }
    [StringLength(100)]
    public string? TenNguoiNhan { get; set; }
    [StringLength(20)]
    public string? SoDienThoaiGiaoHang { get; set; }
    [StringLength(255)]
    public string? DiaChiGiaoHang { get; set; }
    [StringLength(50)]
    public string? PhuongThucThanhToan { get; set; }
    [StringLength(50)]
    public string? MaVoucher { get; set; }
    public decimal TienGiamGia { get; set; }
    [StringLength(50)]
    public string? MaNVQuanLyCongNo { get; set; }
    public List<ChiTietDonHang> ChiTiet { get; set; } = new();
}
