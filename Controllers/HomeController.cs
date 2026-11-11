using Microsoft.AspNetCore.Mvc;
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

        public IActionResult Index()
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
            return View();
        }
    }
}
