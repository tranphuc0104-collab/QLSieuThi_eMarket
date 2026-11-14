using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

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
