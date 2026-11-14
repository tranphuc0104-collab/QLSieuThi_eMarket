using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EMarket.Models;

public class ProductFormViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã sản phẩm.")]
    [StringLength(50, ErrorMessage = "Mã sản phẩm không được vượt quá 50 ký tự.")]
    [Display(Name = "Mã sản phẩm")]
    public string MaSP { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Danh mục")]
    public string? MaDanhMuc { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
    [StringLength(100, ErrorMessage = "Tên sản phẩm không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên sản phẩm")]
    public string TenSP { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Mô tả không được vượt quá 255 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? MoTa { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Giá bán phải lớn hơn 0.")]
    [Display(Name = "Giá bán (đ)")]
    public decimal GiaBan { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Hạn sử dụng")]
    public DateTime? HanSuDung { get; set; }

    [StringLength(50, ErrorMessage = "Đơn vị tính không được vượt quá 50 ký tự.")]
    [Display(Name = "Đơn vị tính")]
    public string? DonViTinh { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho không được là số âm.")]
    [Display(Name = "Tồn kho")]
    public int SoLuongTon { get; set; }

    [Display(Name = "Ảnh sản phẩm")]
    public IFormFile? Image { get; set; }

    public string? ExistingImage { get; set; }
}
