namespace EMarket.Models;

public class BaoCaoViewModel
{
    public string KyBaoCao { get; set; } = "thang";
    public DateTime TuNgay { get; set; }
    public DateTime DenNgay { get; set; }
    public string? MaSP { get; set; }
    public string? MaNCC { get; set; }
    public List<SanPham> SanPhams { get; set; } = new();
    public List<NhaCungCap> NhaCungCaps { get; set; } = new();
    public int SoDonBan { get; set; }
    public decimal DoanhThu { get; set; }
    public int SoLuongBan { get; set; }
    public int SoPhieuNhap { get; set; }
    public decimal TienNhap { get; set; }
    public int SoLuongNhap { get; set; }
    public List<BaoCaoMatHangViewModel> MatHangs { get; set; } = new();
    public List<BaoCaoNhaCungCapViewModel> NhaCungCapRows { get; set; } = new();
    public List<BaoCaoThoiGianViewModel> ThoiGianRows { get; set; } = new();
}
