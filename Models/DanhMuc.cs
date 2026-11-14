using System.ComponentModel.DataAnnotations;

namespace EMarket.Models;

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
