using Microsoft.AspNetCore.Mvc;
using EMarket.Models;
using EMarket.Services;

namespace EMarket.Controllers
{
    public class HomeController : Controller
    {
        private readonly DataStore _db;

        public HomeController(DataStore db)
        {
            _db = db;
        }

        public IActionResult Index(string? keyword)
        {
            if (!User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "CuaHang");
            }

            ViewBag.TotalOrders = _db.DonHangs.Count;
            ViewBag.TotalRevenue = _db.DonHangs.Sum(d => d.TongTien);
            ViewBag.TotalCustomers = _db.KhachHangs.Count;
            ViewBag.TotalProducts = _db.SanPhams.Count;
            ViewBag.WarningStock = _db.TonKhos.Count(t => t.SoLuongTon < t.MucToiThieu || t.SoLuongTon > t.MucToiDa);
            ViewBag.DebtWarning = _db.CongNos.Count(c => c.TrangThai == "Quá hạn");

            ViewBag.RecentOrders = _db.DonHangs.OrderByDescending(d => d.NgayDat).Take(5).ToList();
            var lookup = new CustomerDebtLookupViewModel { Keyword = keyword };
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var searchTerm = keyword.Trim();
                lookup.Customers = _db.KhachHangs
                    .Where(customer =>
                        customer.MaKhachHang.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        customer.HoTen.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (customer.SoDienThoai?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Select(customer =>
                    {
                        var debts = _db.CongNos
                            .Where(debt => debt.MaKhachHang == customer.MaKhachHang &&
                                           debt.LoaiCongNo == "Phải thu" &&
                                           debt.SoTienNo > 0)
                            .OrderByDescending(debt => debt.NgayPhatSinh)
                            .ToList();

                        return new CustomerDebtSummaryViewModel
                        {
                            Customer = customer,
                            Debts = debts,
                            TotalDebt = debts.Sum(debt => debt.SoTienNo),
                            OverdueDebt = debts
                                .Where(debt => debt.TrangThai == "Quá hạn" ||
                                               (debt.HanThanhToan.HasValue && debt.HanThanhToan.Value.Date < DateTime.Today))
                                .Sum(debt => debt.SoTienNo)
                        };
                    })
                    .ToList();
            }

            ViewBag.CustomerLookup = lookup;
            return View();
        }
    }
}
