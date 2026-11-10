using System.ComponentModel.DataAnnotations;

namespace EMarket.Models
{
    public class LoaiKhachHang
    {
        [Key]
        [StringLength(50)]
        public string MaLoai { get; set; } = string.Empty;
        [Required]
        [StringLength(100)]
        public string TenLoai { get; set; } = string.Empty;
        [StringLength(255)]
        public string? MoTa { get; set; }
    }

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

    public class TheThanhVien
    {
        [Key]
        [StringLength(50)]
        public string MaThe { get; set; } = string.Empty;
        [StringLength(50)]
        public string? MaKhachHang { get; set; }
        public DateTime? NgayLap { get; set; }
        public int DiemTichLuy { get; set; } = 0;
    }

    public class DanhMuc
    {
        [Key]
        [StringLength(50)]
        public string MaDanhMuc { get; set; } = string.Empty;
        [Required]
        [StringLength(100)]
        public string TenDanhMuc { get; set; } = string.Empty;
        [StringLength(255)]
        public string? MoTa { get; set; }
    }

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

    public class Kho
    {
        [Key]
        [StringLength(50)]
        public string MaKho { get; set; } = string.Empty;
        [Required]
        [StringLength(100)]
        public string TenKho { get; set; } = string.Empty;
        [StringLength(255)]
        public string? DiaChi { get; set; }
    }

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

        // Helper
        public string? TenDanhMuc { get; set; }
        public int SoLuongTon { get; set; }
    }

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
        public List<ChiTietDonHang> ChiTiet { get; set; } = new();
    }

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

    public class TonKho
    {
        [StringLength(50)]
        public string MaKho { get; set; } = string.Empty;
        [StringLength(50)]
        public string MaSP { get; set; } = string.Empty;
        public int SoLuongTon { get; set; } = 0;
        public int SoLuongDaBan { get; set; } = 0;
        public int MucToiThieu { get; set; } = 0;
        public int MucToiDa { get; set; } = 0;

        public string? TenKho { get; set; }
        public string? TenSP { get; set; }
        public string? DonViTinh { get; set; }
        public string? TrangThaiCanhBao { get; set; } // "DuoiToiThieu", "VuotToiDa", "BinhThuong"
    }

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
        public string? LoaiCongNo { get; set; } // "Phải thu" (KH) hoặc "Phải trả" (NCC)
        public DateTime? NgayPhatSinh { get; set; }
        public decimal SoTienNo { get; set; }
        public DateTime? HanThanhToan { get; set; }
        [StringLength(50)]
        public string? TrangThai { get; set; }

        public string? DoiTuongTen { get; set; } // Tên khách hàng hoặc tên NCC
        public string? CanhBao { get; set; } // "Quá hạn", "Vượt mức", "Bình thường"
    }
}
