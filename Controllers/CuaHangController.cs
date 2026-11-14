using EMarket.Models;
using EMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMarket.Controllers;

public class CuaHangController : Controller
{
    private readonly DataStore _db;
    private readonly SqlDataStore _sqlDataStore;

    public CuaHangController(DataStore db, SqlDataStore sqlDataStore)
    {
        _db = db;
        _sqlDataStore = sqlDataStore;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? keyword)
    {
        var initialCart = new List<object>();
        if (User.IsInRole("Customer"))
        {
            var savedCart = await _sqlDataStore.GetCartAsync(GetOrCreateCartSessionId());
            initialCart = savedCart.Select(item =>
            {
                var product = _db.SanPhams.FirstOrDefault(candidate => candidate.MaSP == item.Key);
                return product == null ? null : new
                {
                    id = item.Key,
                    name = product.TenSP,
                    price = product.GiaBan,
                    quantity = item.Value
                };
            }).Where(item => item != null).Cast<object>().ToList();
        }
        ViewBag.InitialCartJson = System.Text.Json.JsonSerializer.Serialize(initialCart);

        var customerId = User.FindFirst("CustomerId")?.Value;
        ViewBag.ActiveVouchers = _db.Vouchers
            .Where(voucher => voucher.MaKhachHang == customerId &&
                voucher.MaDonHangSuDung == null && voucher.NgayHetHan.Date >= DateTime.Today)
            .OrderBy(voucher => voucher.NgayHetHan)
            .ToList();
        IEnumerable<SanPham> products = _db.SanPhams;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            products = products.Where(product =>
                product.TenSP.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                product.MaSP.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (product.MoTa?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        ViewBag.Keyword = keyword;
        return View(products.ToList());
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCart([FromBody] Dictionary<string, int>? items)
    {
        if (items == null || items.Count > 100 ||
            items.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 50 ||
                item.Value is < 1 or > 9999 || !_db.SanPhams.Any(product => product.MaSP == item.Key)))
        {
            return BadRequest(new { message = "Giỏ hàng không hợp lệ hoặc có sản phẩm không tồn tại." });
        }

        await _sqlDataStore.SaveCartAsync(GetOrCreateCartSessionId(), items);
        return Ok();
    }

    private string GetOrCreateCartSessionId()
    {
        var existingId = Request.Cookies["emarket-cart-id"];
        if (Guid.TryParseExact(existingId, "N", out _))
        {
            return existingId!;
        }

        var sessionId = Guid.NewGuid().ToString("N");
        Response.Cookies.Append("emarket-cart-id", sessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });
        return sessionId;
    }
}
