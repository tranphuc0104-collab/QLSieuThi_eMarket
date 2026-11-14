using EMarket.Models;
using EMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EMarket.Controllers;

[Authorize(Roles = "Admin")]
public class QuanLySanPhamController : Controller
{
    private readonly DataStore _store;
    private readonly SqlDataStore _sqlDataStore;
    private readonly ProductImageStorage _imageStorage;
    private readonly ILogger<QuanLySanPhamController> _logger;

    public QuanLySanPhamController(
        DataStore store,
        SqlDataStore sqlDataStore,
        ProductImageStorage imageStorage,
        ILogger<QuanLySanPhamController> logger)
    {
        _store = store;
        _sqlDataStore = sqlDataStore;
        _imageStorage = imageStorage;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View(_store.SanPhams.OrderBy(product => product.MaSP).ToList());
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetCategories();
        ViewBag.IsEdit = false;
        return View("Form", new ProductFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model)
    {
        ValidateCategory(model);
        if (!ModelState.IsValid)
        {
            PrepareForm(model, false);
            return View("Form", model);
        }

        string? uploadedImage = null;
        try
        {
            if (model.Image is { Length: > 0 })
            {
                uploadedImage = await _imageStorage.SaveAsync(model.Image);
            }

            var product = CreateProduct(model, uploadedImage);
            await _sqlDataStore.AddProductAsync(product, _store);
            TempData["Success"] = "Đã thêm sản phẩm.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(model.Image), exception.Message);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Không thể lưu ảnh sản phẩm {ProductId}.", model.MaSP);
            ModelState.AddModelError(nameof(model.Image), "Không thể lưu ảnh. Vui lòng thử lại.");
        }
        catch (InvalidOperationException exception)
        {
            DeleteImageAfterFailure(uploadedImage);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (SqlException exception)
        {
            DeleteImageAfterFailure(uploadedImage);
            _logger.LogError(exception, "Không thể thêm sản phẩm {ProductId} vào cơ sở dữ liệu.", model.MaSP);
            ModelState.AddModelError(string.Empty, exception.Number is 2601 or 2627
                ? "Mã sản phẩm đã tồn tại."
                : "Không thể lưu sản phẩm vào cơ sở dữ liệu. Vui lòng thử lại.");
        }

        PrepareForm(model, false);
        return View("Form", model);
    }

    [HttpGet]
    public IActionResult Edit(string id)
    {
        var product = _store.SanPhams.FirstOrDefault(item => item.MaSP == id);
        if (product == null)
        {
            return NotFound();
        }

        SetCategories();
        ViewBag.IsEdit = true;
        return View("Form", ToFormModel(product));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductFormViewModel model)
    {
        var existing = _store.SanPhams.FirstOrDefault(item => item.MaSP == model.MaSP);
        if (existing == null)
        {
            return NotFound();
        }

        ValidateCategory(model);
        if (!ModelState.IsValid)
        {
            PrepareForm(model, true);
            return View("Form", model);
        }

        var previousImage = existing.HinhAnh;
        var imagePath = previousImage;
        string? uploadedImage = null;
        try
        {
            if (model.Image is { Length: > 0 })
            {
                uploadedImage = await _imageStorage.SaveAsync(model.Image);
                imagePath = uploadedImage;
            }

            var product = CreateProduct(model, imagePath);
            if (!await _sqlDataStore.UpdateProductAsync(product, _store))
            {
                DeleteImageAfterFailure(uploadedImage);
                return NotFound();
            }

            var oldImageDeleted = uploadedImage == null || RemoveImageSafely(previousImage);
            TempData["Success"] = oldImageDeleted
                ? "Đã cập nhật sản phẩm."
                : "Đã cập nhật sản phẩm nhưng chưa thể xóa ảnh cũ khỏi thư mục.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(nameof(model.Image), exception.Message);
        }
        catch (IOException exception)
        {
            DeleteImageAfterFailure(uploadedImage);
            _logger.LogError(exception, "Không thể lưu ảnh sản phẩm {ProductId}.", model.MaSP);
            ModelState.AddModelError(nameof(model.Image), "Không thể lưu ảnh. Vui lòng thử lại.");
        }
        catch (InvalidOperationException exception)
        {
            DeleteImageAfterFailure(uploadedImage);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (SqlException exception)
        {
            DeleteImageAfterFailure(uploadedImage);
            _logger.LogError(exception, "Không thể cập nhật sản phẩm {ProductId} trong cơ sở dữ liệu.", model.MaSP);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật sản phẩm trong cơ sở dữ liệu. Vui lòng thử lại.");
        }

        model.ExistingImage = previousImage;
        PrepareForm(model, true);
        return View("Form", model);
    }

    [HttpGet]
    public IActionResult Delete(string id)
    {
        var product = _store.SanPhams.FirstOrDefault(item => item.MaSP == id);
        return product == null ? NotFound() : View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var product = _store.SanPhams.FirstOrDefault(item => item.MaSP == id);
        if (product == null)
        {
            return NotFound();
        }

        try
        {
            if (!await _sqlDataStore.DeleteProductAsync(id, _store))
            {
                return NotFound();
            }
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Không thể xóa sản phẩm {ProductId} khỏi cơ sở dữ liệu.", id);
            TempData["Error"] = "Không thể xóa sản phẩm khỏi cơ sở dữ liệu. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = RemoveImageSafely(product.HinhAnh)
            ? "Đã xóa sản phẩm."
            : "Đã xóa sản phẩm nhưng chưa thể xóa ảnh khỏi thư mục.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateCategory(ProductFormViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.MaDanhMuc) &&
            !_store.DanhMucs.Any(category => category.MaDanhMuc == model.MaDanhMuc))
        {
            ModelState.AddModelError(nameof(model.MaDanhMuc), "Danh mục đã chọn không tồn tại.");
        }
    }

    private void PrepareForm(ProductFormViewModel model, bool isEdit)
    {
        SetCategories();
        ViewBag.IsEdit = isEdit;
        model.ExistingImage ??= isEdit
            ? _store.SanPhams.FirstOrDefault(item => item.MaSP == model.MaSP)?.HinhAnh
            : null;
    }

    private void SetCategories()
    {
        ViewBag.Categories = _store.DanhMucs.OrderBy(category => category.TenDanhMuc).ToList();
    }

    private static ProductFormViewModel ToFormModel(SanPham product)
    {
        return new ProductFormViewModel
        {
            MaSP = product.MaSP,
            MaDanhMuc = product.MaDanhMuc,
            TenSP = product.TenSP,
            MoTa = product.MoTa,
            GiaBan = product.GiaBan,
            HanSuDung = product.HanSuDung,
            DonViTinh = product.DonViTinh,
            SoLuongTon = product.SoLuongTon,
            ExistingImage = product.HinhAnh
        };
    }

    private static SanPham CreateProduct(ProductFormViewModel model, string? imagePath)
    {
        return new SanPham
        {
            MaSP = model.MaSP.Trim(),
            MaDanhMuc = string.IsNullOrWhiteSpace(model.MaDanhMuc) ? null : model.MaDanhMuc,
            TenSP = model.TenSP.Trim(),
            MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa.Trim(),
            GiaBan = model.GiaBan,
            HanSuDung = model.HanSuDung,
            DonViTinh = string.IsNullOrWhiteSpace(model.DonViTinh) ? null : model.DonViTinh.Trim(),
            SoLuongTon = model.SoLuongTon,
            HinhAnh = imagePath
        };
    }

    private void DeleteImageAfterFailure(string? imagePath)
    {
        if (imagePath != null)
        {
            RemoveImageSafely(imagePath);
        }
    }

    private bool RemoveImageSafely(string? imagePath)
    {
        try
        {
            _imageStorage.Delete(imagePath);
            return true;
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Không thể xóa tệp ảnh sản phẩm {ImagePath}.", imagePath);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Không có quyền xóa tệp ảnh sản phẩm {ImagePath}.", imagePath);
            return false;
        }
    }
}
