using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

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
