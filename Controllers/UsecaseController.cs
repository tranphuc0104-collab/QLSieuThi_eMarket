using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using EMarket.Services;
using EMarket.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace EMarket.Controllers
{
    [Authorize]
    public class UsecaseController : Controller
    {
        private readonly DataStore _db;
        private readonly SqlDataStore _sqlDataStore;

        public UsecaseController(DataStore db, SqlDataStore sqlDataStore)
        {
            _db = db;
            _sqlDataStore = sqlDataStore;
        }

        // ==========================================
        // 1. UC1: THEO DÕI QUÁ TRÌNH XỬ LÝ ĐƠN HÀNG
        // ==========================================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> TheoDoiDonHang(string? maDonHang)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var orders = await _sqlDataStore.GetCustomerOrdersAsync(customerId);
            ViewBag.AllOrders = orders;
            if (string.IsNullOrWhiteSpace(maDonHang))
            {
                return View(null);
            }

            var order = orders.FirstOrDefault(d =>
                d.MaDonHang.Trim().Equals(maDonHang.Trim(), StringComparison.OrdinalIgnoreCase));
            if (order == null)
            {
                ViewBag.Error = $"Không tìm thấy đơn hàng tương ứng với mã '{maDonHang}'. Vui lòng kiểm tra lại.";
                ViewBag.SearchCode = maDonHang;
                return View(null);
            }

            ViewBag.SearchCode = maDonHang;
            return View(order);
        }

        // ==========================================
        // 2. UC2: ĐẶT HÀNG (Khách hàng)
        // ==========================================
        [Authorize(Roles = "Customer")]
        public IActionResult DatHang()
        {
            return RedirectToAction("Index", "CuaHang");
        }

        [Authorize(Roles = "Customer")]
        [HttpPost]
        public IActionResult DatHang(string maKhachHang, string maSP, int soLuong, string? ghiChu)
        {
            TempData["Error"] = "Vui lòng thêm sản phẩm vào giỏ hàng và hoàn tất thông tin giao hàng để đặt đơn.";
            return RedirectToAction("Index", "CuaHang");
        }

        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DatHangTuGioHang(
            List<string>? maSP,
            List<int>? soLuong,
            string? tenNguoiNhan,
            string? soDienThoaiGiaoHang,
            string? diaChiGiaoHang,
            string? phuongThucThanhToan,
            string? maVoucher)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            var customer = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == customerId);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy hồ sơ khách hàng. Vui lòng liên hệ siêu thị để được hỗ trợ.";
                return RedirectToAction("Index", "CuaHang");
            }

            tenNguoiNhan = tenNguoiNhan?.Trim();
            soDienThoaiGiaoHang = soDienThoaiGiaoHang?.Trim();
            diaChiGiaoHang = diaChiGiaoHang?.Trim();
            if (string.IsNullOrWhiteSpace(tenNguoiNhan) || tenNguoiNhan.Length > 100 ||
                string.IsNullOrWhiteSpace(soDienThoaiGiaoHang) ||
                !System.Text.RegularExpressions.Regex.IsMatch(soDienThoaiGiaoHang, @"^[0-9+().\-\s]{8,20}$") ||
                string.IsNullOrWhiteSpace(diaChiGiaoHang) || diaChiGiaoHang.Length > 255 ||
                maVoucher is { Length: > 50 } ||
                phuongThucThanhToan is not ("Thanh toán khi nhận hàng" or "Chuyển khoản ngân hàng" or "Ghi nợ"))
            {
                TempData["Error"] = "Vui lòng nhập tên, số điện thoại, địa chỉ giao hàng hợp lệ và chọn phương thức thanh toán.";
                return RedirectToAction("Index", "CuaHang");
            }

            if (maSP == null || soLuong == null || maSP.Count == 0 ||
                maSP.Count != soLuong.Count || maSP.Count > 100)
            {
                TempData["Error"] = "Giỏ hàng không hợp lệ hoặc đang trống. Vui lòng kiểm tra lại giỏ hàng.";
                return RedirectToAction("Index", "CuaHang");
            }

            var requestedQuantities = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < maSP.Count; index++)
            {
                var productId = maSP[index]?.Trim();
                if (string.IsNullOrWhiteSpace(productId) || soLuong[index] <= 0)
                {
                    TempData["Error"] = "Sản phẩm hoặc số lượng trong giỏ hàng không hợp lệ.";
                    return RedirectToAction("Index", "CuaHang");
                }

                requestedQuantities[productId] = requestedQuantities.GetValueOrDefault(productId) + soLuong[index];
            }

            var orderLines = new List<ChiTietDonHang>();
            foreach (var request in requestedQuantities)
            {
                var product = _db.SanPhams.FirstOrDefault(s =>
                    s.MaSP.Equals(request.Key, StringComparison.OrdinalIgnoreCase));
                if (product == null)
                {
                    TempData["Error"] = $"Sản phẩm có mã '{request.Key}' không còn tồn tại. Vui lòng cập nhật giỏ hàng.";
                    return RedirectToAction("Index", "CuaHang");
                }

                if (request.Value > product.SoLuongTon)
                {
                    TempData["Error"] = $"Sản phẩm '{product.TenSP}' chỉ còn {product.SoLuongTon} {product.DonViTinh} trong kho. Vui lòng điều chỉnh số lượng.";
                    return RedirectToAction("Index", "CuaHang");
                }

                var quantity = (int)request.Value;
                orderLines.Add(new ChiTietDonHang
                {
                    MaSP = product.MaSP,
                    TenSP = product.TenSP,
                    SoLuong = quantity,
                    DonGia = product.GiaBan,
                    ThanhTien = quantity * product.GiaBan,
                    DonViTinh = product.DonViTinh
                });
            }

            var newId = "DH" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N")[..6];
            foreach (var line in orderLines)
            {
                line.MaDonHang = newId;
            }

            var order = new DonHang
            {
                MaDonHang = newId,
                MaKhachHang = customer.MaKhachHang,
                MaNV = customer.MaNV ?? "NV02",
                NgayDat = DateTime.Now,
                TrangThai = "Đang xử lý",
                TongTien = orderLines.Sum(line => line.ThanhTien),
                TenKhachHang = tenNguoiNhan,
                TenNguoiNhan = tenNguoiNhan,
                SoDienThoai = soDienThoaiGiaoHang,
                SoDienThoaiGiaoHang = soDienThoaiGiaoHang,
                DiaChiGiaoHang = diaChiGiaoHang,
                PhuongThucThanhToan = phuongThucThanhToan,
                MaVoucher = string.IsNullOrWhiteSpace(maVoucher) ? null : maVoucher.Trim(),
                MaNVQuanLyCongNo = _db.NhanViens
                    .FirstOrDefault(employee => employee.VaiTro != null &&
                        employee.VaiTro.Contains("công nợ", StringComparison.OrdinalIgnoreCase))?.MaNV,
                ChiTiet = orderLines
            };

            var cartSessionId = Request.Cookies["emarket-cart-id"] ?? string.Empty;
            var saveError = await _sqlDataStore.SaveCustomerOrderAsync(order, cartSessionId);
            if (saveError != null)
            {
                TempData["Error"] = saveError;
                return RedirectToAction("Index", "CuaHang");
            }

            foreach (var line in orderLines)
            {
                var product = _db.SanPhams.First(s => s.MaSP == line.MaSP);
                product.SoLuongTon -= line.SoLuong;

                var remainingQuantity = line.SoLuong;
                foreach (var inventory in _db.TonKhos
                    .Where(t => t.MaSP == line.MaSP && t.SoLuongTon > 0)
                    .OrderBy(t => t.MaKho))
                {
                    var deductedQuantity = Math.Min(remainingQuantity, inventory.SoLuongTon);
                    inventory.SoLuongTon -= deductedQuantity;
                    inventory.SoLuongDaBan += deductedQuantity;
                    remainingQuantity -= deductedQuantity;
                    if (remainingQuantity == 0)
                    {
                        break;
                    }
                }
            }

            var pointsEarned = (int)(order.TongTien / 10000);
            customer.DiemTichLuy += pointsEarned;
            var memberCard = _db.TheThanhViens.FirstOrDefault(card => card.MaKhachHang == customer.MaKhachHang);
            if (memberCard != null)
            {
                memberCard.DiemTichLuy += pointsEarned;
            }

            if (phuongThucThanhToan == "Ghi nợ")
            {
                _db.CongNos.Insert(0, new CongNo
                {
                    MaCongNo = "CN-" + newId,
                    MaNV = order.MaNVQuanLyCongNo,
                    MaKhachHang = customer.MaKhachHang,
                    MaDonHang = newId,
                    LoaiCongNo = "Phải thu",
                    NgayPhatSinh = order.NgayDat,
                    SoTienNo = order.TongTien,
                    HanThanhToan = order.NgayDat?.Date.AddDays(30),
                    TrangThai = "Chưa trả",
                    DoiTuongTen = customer.HoTen,
                    CanhBao = "Bình thường"
                });
            }

            _db.DonHangs.Insert(0, order);
            if (order.MaVoucher != null)
            {
                var voucher = _db.Vouchers.FirstOrDefault(item =>
                    item.MaVoucher == order.MaVoucher && item.MaKhachHang == customer.MaKhachHang &&
                    item.MaDonHangSuDung == null);
                if (voucher != null) voucher.MaDonHangSuDung = order.MaDonHang;
            }
            _db.ChiTietDonHangs.AddRange(orderLines);
            TempData["Success"] = $"Đặt hàng thành công! Mã đơn hàng: {newId}. Bạn nhận được {pointsEarned} điểm. Bạn có thể tiếp tục mua sắm để tạo đơn hàng khác.";
            TempData["ClearCart"] = true;
            TempData["NewOrderId"] = newId;
            return RedirectToAction("Index", "CuaHang");
        }

        // ==========================================
        // 3. UC3: TRA CỨU THÔNG TIN HÀNG HÓA
        // ==========================================
        [AllowAnonymous]
        public IActionResult TraCuuHangHoa(string? keyword, string? danhMuc)
        {
            ViewBag.DanhMucs = _db.DanhMucs.ToList();
            ViewBag.Keyword = keyword;
            ViewBag.SelectedDM = danhMuc;

            var query = _db.SanPhams.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(s => s.MaSP.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                         s.TenSP.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                         (s.MoTa != null && s.MoTa.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(danhMuc))
            {
                query = query.Where(s => s.MaDanhMuc == danhMuc);
            }

            var result = query.ToList();
            if (keyword != null && !result.Any())
            {
                ViewBag.NotFound = true;
            }

            return View(result);
        }

        // ==========================================
        // 4. UC4: QUẢN LÝ BÁN HÀNG (Nhân viên)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult BanHang()
        {
            ViewBag.KhachHangs = _db.KhachHangs.ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();
            ViewBag.RecentOrders = _db.DonHangs.OrderByDescending(d => d.NgayDat).Take(5).ToList();
            return View();
        }

        [Authorize(Roles = "Admin")]
        public IActionResult HoaDon(string? maDonHang)
        {
            var orders = _db.DonHangs.OrderByDescending(d => d.NgayDat).ToList();
            ViewBag.SelectedOrder = string.IsNullOrWhiteSpace(maDonHang)
                ? null
                : orders.FirstOrDefault(d => d.MaDonHang.Equals(maDonHang.Trim(), StringComparison.OrdinalIgnoreCase));
            ViewBag.SearchCode = maDonHang;
            return View(orders);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult XuLyDonHang(string? trangThai, string? maDonHang)
        {
            ViewBag.SelectedStatus = trangThai;
            ViewBag.SearchCode = maDonHang;
            IEnumerable<DonHang> orders = _db.DonHangs.OrderByDescending(order => order.NgayDat);
            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                orders = orders.Where(order => order.TrangThai == trangThai);
            }

            if (!string.IsNullOrWhiteSpace(maDonHang))
            {
                orders = orders.Where(order => order.MaDonHang.Contains(maDonHang.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return View(orders.ToList());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatTrangThaiDonHang(string maDonHang, string trangThaiMoi)
        {
            var order = _db.DonHangs.FirstOrDefault(item =>
                item.MaDonHang.Equals(maDonHang, StringComparison.OrdinalIgnoreCase));
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng cần cập nhật.";
                return RedirectToAction(nameof(XuLyDonHang));
            }

            var nextStatus = order.TrangThai switch
            {
                "Đang xử lý" => "Đang chuẩn bị",
                "Đang chuẩn bị" => "Đang giao",
                "Đang giao" => "Đã hoàn tất",
                _ => null
            };
            if (nextStatus == null || trangThaiMoi != nextStatus)
            {
                TempData["Error"] = "Đơn hàng chỉ được chuyển sang bước tiếp theo trong quy trình xử lý.";
                return RedirectToAction(nameof(XuLyDonHang), new { trangThai = order.TrangThai });
            }

            if (!await _sqlDataStore.UpdateOrderStatusAsync(order.MaDonHang, order.TrangThai!, trangThaiMoi))
            {
                TempData["Error"] = "Đơn hàng vừa được cập nhật ở nơi khác. Hãy tải lại danh sách trước khi xử lý tiếp.";
                return RedirectToAction(nameof(XuLyDonHang));
            }

            order.TrangThai = trangThaiMoi;
            TempData["Success"] = $"Đơn hàng '{order.MaDonHang}' đã chuyển sang trạng thái '{trangThaiMoi}'.";
            return RedirectToAction(nameof(XuLyDonHang), new { trangThai = trangThaiMoi });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LapDonBanHang(string? maKhachHang, List<string>? maSP, List<int>? soLuong, List<decimal?>? giaApDung)
        {
            ViewBag.KhachHangs = _db.KhachHangs.ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();
            ViewBag.RecentOrders = _db.DonHangs.OrderByDescending(d => d.NgayDat).Take(5).ToList();

            maSP ??= new List<string>();
            soLuong ??= new List<int>();
            giaApDung ??= new List<decimal?>();
            if (string.IsNullOrWhiteSpace(maKhachHang) || maSP.Count == 0 ||
                maSP.Count != soLuong.Count || maSP.Count != giaApDung.Count)
            {
                ViewBag.Error = "Thông tin đơn hàng không hợp lệ! Vui lòng chọn khách hàng và ít nhất một sản phẩm.";
                return View("BanHang");
            }

            var customer = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == maKhachHang);
            if (customer == null)
            {
                ViewBag.Error = "Khách hàng không tồn tại trong hệ thống.";
                return View("BanHang");
            }

            var orderLines = new List<ChiTietDonHang>();
            var selectedProductIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < maSP.Count; index++)
            {
                var productId = maSP[index]?.Trim();
                if (string.IsNullOrEmpty(productId) || soLuong[index] <= 0 ||
                    !selectedProductIds.Add(productId))
                {
                    ViewBag.Error = "Mỗi dòng phải chọn một sản phẩm khác nhau và có số lượng lớn hơn 0.";
                    return View("BanHang");
                }

                var product = _db.SanPhams.FirstOrDefault(s =>
                    s.MaSP.Equals(productId, StringComparison.OrdinalIgnoreCase));
                if (product == null)
                {
                    ViewBag.Error = $"Sản phẩm '{productId}' không tồn tại trong hệ thống.";
                    return View("BanHang");
                }

                if (product.SoLuongTon < soLuong[index])
                {
                    ViewBag.Error = $"Kho không đủ '{product.TenSP}'! Tồn kho hiện tại: {product.SoLuongTon}. Yêu cầu xuất: {soLuong[index]}.";
                    return View("BanHang");
                }

                var discountRate = customer.MaLoai switch
                {
                    "LKH03" => 0.10m,
                    "LKH02" => 0.05m,
                    _ => 0m
                };
                var unitPrice = giaApDung[index] ?? product.GiaBan * (1 - discountRate);
                if (unitPrice <= 0)
                {
                    ViewBag.Error = $"Giá bán của '{product.TenSP}' phải lớn hơn 0.";
                    return View("BanHang");
                }

                orderLines.Add(new ChiTietDonHang
                {
                    MaSP = product.MaSP,
                    TenSP = product.TenSP,
                    SoLuong = soLuong[index],
                    DonGia = unitPrice,
                    ThanhTien = soLuong[index] * unitPrice,
                    DonViTinh = product.DonViTinh
                });
            }

            var newId = "DH" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N")[..6];
            foreach (var line in orderLines)
            {
                line.MaDonHang = newId;
            }

            var donHang = new DonHang
            {
                MaDonHang = newId,
                MaKhachHang = maKhachHang,
                MaNV = "NV02", // NV Bán hàng
                NgayDat = DateTime.Now,
                TrangThai = "Đã hoàn tất",
                TongTien = orderLines.Sum(line => line.ThanhTien),
                TenKhachHang = customer.HoTen,
                SoDienThoai = customer.SoDienThoai,
                ChiTiet = orderLines
            };

            var saveError = await _sqlDataStore.SavePosOrderAsync(donHang);
            if (saveError != null)
            {
                ViewBag.Error = saveError;
                return View("BanHang");
            }

            foreach (var line in orderLines)
            {
                var product = _db.SanPhams.First(s => s.MaSP == line.MaSP);
                product.SoLuongTon -= line.SoLuong;
                var remaining = line.SoLuong;
                foreach (var inventory in _db.TonKhos
                    .Where(t => t.MaSP == line.MaSP && t.SoLuongTon > 0)
                    .OrderBy(t => t.MaKho))
                {
                    var deduction = Math.Min(remaining, inventory.SoLuongTon);
                    inventory.SoLuongTon -= deduction;
                    inventory.SoLuongDaBan += deduction;
                    remaining -= deduction;
                    if (remaining == 0) break;
                }
            }

            var diemCong = (int)(donHang.TongTien / 10000);
            customer.DiemTichLuy += diemCong;
            var the = _db.TheThanhViens.FirstOrDefault(t => t.MaKhachHang == customer.MaKhachHang);
            if (the != null) the.DiemTichLuy += diemCong;

            _db.DonHangs.Insert(0, donHang);
            _db.ChiTietDonHangs.AddRange(donHang.ChiTiet);

            ViewBag.Success = $"Lập và lưu hóa đơn thành công! Mã hóa đơn: {newId}. Đã bán {orderLines.Count} sản phẩm, tổng tiền {donHang.TongTien:N0} VNĐ và cộng +{diemCong} điểm cho khách hàng.";
            ViewBag.RecentOrders = _db.DonHangs.OrderByDescending(d => d.NgayDat).Take(5).ToList();
            return View("BanHang");
        }

        // ==========================================
        // 5. UC5: QUẢN LÝ THÔNG TIN KHÁCH HÀNG (CRUD)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult KhachHang()
        {
            ViewBag.LoaiKhachHangs = _db.LoaiKhachHangs.ToList();
            ViewBag.NhanViens = _db.NhanViens.ToList();
            return View(_db.KhachHangs.ToList());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemKhachHang(KhachHang kh)
        {
            if (string.IsNullOrWhiteSpace(kh.MaKhachHang) || string.IsNullOrWhiteSpace(kh.HoTen))
            {
                TempData["Error"] = "Vui lòng nhập Mã khách hàng và Họ tên!";
                return RedirectToAction("KhachHang");
            }

            if (_db.KhachHangs.Any(k => k.MaKhachHang.Equals(kh.MaKhachHang, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = $"Mã khách hàng '{kh.MaKhachHang}' đã tồn tại trong hệ thống!";
                return RedirectToAction("KhachHang");
            }

            var loai = _db.LoaiKhachHangs.FirstOrDefault(l => l.MaLoai == kh.MaLoai);
            if (loai == null)
            {
                TempData["Error"] = "Vui lòng chọn loại khách hàng hợp lệ.";
                return RedirectToAction("KhachHang");
            }

            kh.TenLoai = loai?.TenLoai ?? "Khách thông thường";
            kh.DiemTichLuy = 0;

            var card = new TheThanhVien
            {
                MaThe = "THE" + kh.MaKhachHang,
                MaKhachHang = kh.MaKhachHang,
                NgayLap = DateTime.Now,
                DiemTichLuy = 0
            };
            await _sqlDataStore.AddCustomerAsync(kh, card);
            _db.KhachHangs.Add(kh);
            _db.TheThanhViens.Add(card);

            TempData["Success"] = $"Thêm khách hàng '{kh.HoTen}' ({kh.MaKhachHang}) thành công!";
            return RedirectToAction("KhachHang");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaKhachHang(KhachHang kh)
        {
            var existing = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == kh.MaKhachHang);
            if (existing == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng cần cập nhật!";
                return RedirectToAction("KhachHang");
            }

            if (!_db.LoaiKhachHangs.Any(item => item.MaLoai == kh.MaLoai) ||
                (kh.MaNV != null && !_db.NhanViens.Any(item => item.MaNV == kh.MaNV)))
            {
                TempData["Error"] = "Loại khách hàng hoặc nhân viên phụ trách không hợp lệ.";
                return RedirectToAction("KhachHang");
            }

            var updated = new KhachHang
            {
                MaKhachHang = existing.MaKhachHang,
                HoTen = kh.HoTen?.Trim() ?? string.Empty,
                SoDienThoai = kh.SoDienThoai?.Trim(),
                Email = kh.Email?.Trim(),
                DiaChi = kh.DiaChi?.Trim(),
                MaLoai = kh.MaLoai,
                MaNV = kh.MaNV
            };
            if (!await _sqlDataStore.UpdateCustomerAsync(updated))
            {
                TempData["Error"] = "Không tìm thấy khách hàng trong cơ sở dữ liệu để cập nhật.";
                return RedirectToAction("KhachHang");
            }

            existing.HoTen = updated.HoTen;
            existing.SoDienThoai = updated.SoDienThoai;
            existing.Email = updated.Email;
            existing.DiaChi = updated.DiaChi;
            existing.MaLoai = updated.MaLoai;
            existing.MaNV = updated.MaNV;
            var loai = _db.LoaiKhachHangs.FirstOrDefault(l => l.MaLoai == kh.MaLoai);
            existing.TenLoai = loai?.TenLoai ?? existing.TenLoai;

            TempData["Success"] = $"Cập nhật khách hàng '{existing.HoTen}' thành công!";
            return RedirectToAction("KhachHang");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaKhachHang(string maKhachHang)
        {
            var kh = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == maKhachHang);
            if (kh == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng cần xóa!";
                return RedirectToAction("KhachHang");
            }

            if (!await _sqlDataStore.DeleteCustomerAsync(maKhachHang))
            {
                TempData["Error"] = "Không thể xóa khách hàng đã phát sinh đơn hàng, công nợ hoặc voucher.";
                return RedirectToAction("KhachHang");
            }

            _db.KhachHangs.Remove(kh);
            var the = _db.TheThanhViens.FirstOrDefault(t => t.MaKhachHang == maKhachHang);
            if (the != null) _db.TheThanhViens.Remove(the);

            TempData["Success"] = $"Đã xóa khách hàng '{kh.HoTen}' khỏi hệ thống.";
            return RedirectToAction("KhachHang");
        }

        // ==========================================
        // 6. UC6: QUẢN LÝ ĐIỂM MUA HÀNG
        // ==========================================
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> DiemMuaHang()
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var customer = await _sqlDataStore.GetCustomerAsync(customerId);
            if (customer == null)
            {
                return NotFound("Không tìm thấy hồ sơ khách hàng.");
            }

            ViewBag.MemberCard = await _sqlDataStore.GetMemberCardAsync(customer.MaKhachHang);
            ViewBag.Orders = await _sqlDataStore.GetCustomerOrdersAsync(customer.MaKhachHang);
            ViewBag.Vouchers = await _sqlDataStore.GetCustomerVouchersAsync(customer.MaKhachHang);
            return View(customer);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TuDoiDiem(int soDiemDoi)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            var customer = customerId == null ? null : await _sqlDataStore.GetCustomerAsync(customerId);
            if (customer == null)
            {
                TempData["Error"] = "Không tìm thấy hồ sơ khách hàng.";
                return RedirectToAction(nameof(DiemMuaHang));
            }

            var result = await RedeemVoucherForCustomerAsync(customer, soDiemDoi);
            TempData[result.Error == null ? "Success" : "Error"] = result.Error ??
                $"Đổi thành công {soDiemDoi} điểm. Mã voucher {result.Voucher!.MaVoucher} giảm {result.Voucher.GiaTri:N0} VNĐ, có hiệu lực đến {result.Voucher.NgayHetHan:dd/MM/yyyy}.";
            return RedirectToAction(nameof(DiemMuaHang));
        }

        private async Task<string?> GetCurrentCustomerIdAsync()
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                return customerId;
            }

            var userName = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            customerId = await _sqlDataStore.GetCustomerIdForAccountAsync(userName);
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return null;
            }

            if (User.Identity is ClaimsIdentity identity)
            {
                identity.AddClaim(new Claim("CustomerId", customerId));
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity));
            }

            return customerId;
        }

        [Authorize(Roles = "Admin")]
        public IActionResult DiemTichLuy(string? maKhachHang)
        {
            ViewBag.KhachHangs = _db.KhachHangs.ToList();
            if (string.IsNullOrWhiteSpace(maKhachHang))
            {
                return View(null);
            }

            var kh = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang.Equals(maKhachHang.Trim(), StringComparison.OrdinalIgnoreCase));
            if (kh == null)
            {
                ViewBag.Error = "Chưa có thông tin điểm cho khách hàng này hoặc mã không hợp lệ.";
                return View(null);
            }

            var orders = _db.DonHangs.Where(d => d.MaKhachHang == kh.MaKhachHang).ToList();
            ViewBag.Orders = orders;
            ViewBag.Vouchers = _db.Vouchers.Where(voucher => voucher.MaKhachHang == kh.MaKhachHang)
                .OrderByDescending(voucher => voucher.NgayTao).ToList();
            return View(kh);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuyDoiDiem(string maKhachHang, int soDiemDoi)
        {
            ViewBag.KhachHangs = _db.KhachHangs.ToList();
            var kh = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == maKhachHang);
            if (kh == null)
            {
                ViewBag.Error = "Khách hàng không tồn tại!";
                return View("DiemTichLuy", null);
            }

            var orders = _db.DonHangs.Where(d => d.MaKhachHang == kh.MaKhachHang).ToList();
            ViewBag.Orders = orders;
            ViewBag.Vouchers = _db.Vouchers.Where(voucher => voucher.MaKhachHang == kh.MaKhachHang)
                .OrderByDescending(voucher => voucher.NgayTao).ToList();

            var result = await RedeemVoucherForCustomerAsync(kh, soDiemDoi);
            if (result.Error != null)
            {
                ViewBag.Error = result.Error;
                return View("DiemTichLuy", kh);
            }

            ViewBag.Vouchers = _db.Vouchers.Where(item => item.MaKhachHang == kh.MaKhachHang)
                .OrderByDescending(item => item.NgayTao).ToList();
            ViewBag.Success = $"Đổi thành công {soDiemDoi} điểm. Mã voucher {result.Voucher!.MaVoucher} giảm {result.Voucher.GiaTri:N0} VNĐ, có hiệu lực đến {result.Voucher.NgayHetHan:dd/MM/yyyy}. Điểm còn lại: {kh.DiemTichLuy}.";
            return View("DiemTichLuy", kh);
        }

        private async Task<(Voucher? Voucher, string? Error)> RedeemVoucherForCustomerAsync(KhachHang customer, int points)
        {
            if (points < 10 || points % 10 != 0)
            {
                return (null, "Số điểm quy đổi phải từ 10 điểm trở lên và theo bội số 10.");
            }

            if (customer.DiemTichLuy < points)
            {
                return (null, $"Không đủ điểm! Khách hàng chỉ có {customer.DiemTichLuy} điểm nhưng yêu cầu đổi {points} điểm.");
            }

            var voucher = new Voucher
            {
                MaVoucher = "EM" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
                MaKhachHang = customer.MaKhachHang,
                GiaTri = points * 1000m,
                NgayTao = DateTime.Today,
                NgayHetHan = DateTime.Today.AddDays(90)
            };
            var remainingPoints = await _sqlDataStore.RedeemVoucherAsync(customer.MaKhachHang, points, voucher);
            if (remainingPoints == null)
            {
                return (null, "Không thể quy đổi: thẻ thành viên không tồn tại hoặc số điểm đã thay đổi. Vui lòng tải lại trang.");
            }

            customer.DiemTichLuy = remainingPoints.Value;
            var cachedCustomer = _db.KhachHangs.FirstOrDefault(item => item.MaKhachHang == customer.MaKhachHang);
            if (cachedCustomer != null) cachedCustomer.DiemTichLuy = remainingPoints.Value;
            var memberCard = _db.TheThanhViens.FirstOrDefault(card => card.MaKhachHang == customer.MaKhachHang);
            if (memberCard != null) memberCard.DiemTichLuy = remainingPoints.Value;
            _db.Vouchers.Insert(0, voucher);
            return (voucher, null);
        }

        // ==========================================
        // 7. UC7: QUẢN LÝ NHẬP HÀNG (Kho)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult NhapHang()
        {
            ViewBag.NhaCungCaps = _db.NhaCungCaps.ToList();
            ViewBag.Khos = _db.Khos.ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();
            ViewBag.RecentImports = _db.PhieuNhaps.OrderByDescending(p => p.NgayNhap).Take(5).ToList();
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoPhieuNhap(string maNCC, string maKho, string maSP, int soLuong, decimal donGiaNhap)
        {
            ViewBag.NhaCungCaps = _db.NhaCungCaps.ToList();
            ViewBag.Khos = _db.Khos.ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();
            ViewBag.RecentImports = _db.PhieuNhaps.OrderByDescending(p => p.NgayNhap).Take(5).ToList();

            if (string.IsNullOrEmpty(maNCC) || string.IsNullOrEmpty(maKho) || string.IsNullOrEmpty(maSP) || soLuong <= 0 || donGiaNhap <= 0)
            {
                ViewBag.Error = "Thông tin không hợp lệ! Vui lòng nhập đầy đủ Nhà cung cấp, Kho nhập, Sản phẩm, Số lượng > 0 và Đơn giá > 0.";
                return View("NhapHang");
            }

            var ncc = _db.NhaCungCaps.FirstOrDefault(n => n.MaNCC == maNCC);
            var kho = _db.Khos.FirstOrDefault(k => k.MaKho == maKho);
            var sp = _db.SanPhams.FirstOrDefault(s => s.MaSP == maSP);
            if (ncc == null || kho == null || sp == null)
            {
                ViewBag.Error = "Nhà cung cấp, kho hoặc sản phẩm đã chọn không tồn tại.";
                return View("NhapHang");
            }

            var newPnId = "PN" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N")[..6];
            decimal thanhTien = soLuong * donGiaNhap;

            var phieuNhap = new PhieuNhap
            {
                MaPhieuNhap = newPnId,
                MaNCC = maNCC,
                MaKho = maKho,
                MaNV = "NV03", // Nhân viên quản lý kho
                NgayNhap = DateTime.Now,
                TongTien = thanhTien,
                TenNCC = ncc?.TenNCC,
                TenKho = kho?.TenKho,
                ChiTiet = new List<ChiTietPhieuNhap>
                {
                    new ChiTietPhieuNhap
                    {
                        MaPhieuNhap = newPnId,
                        MaSP = maSP,
                        TenSP = sp?.TenSP,
                        SoLuong = soLuong,
                        DonGiaNhap = donGiaNhap,
                        ThanhTien = thanhTien
                    }
                }
            };

            await _sqlDataStore.SaveImportAsync(phieuNhap);

            // Cập nhật tồn kho
            if (sp != null)
            {
                sp.SoLuongTon += soLuong;
            }

            var tk = _db.TonKhos.FirstOrDefault(t => t.MaKho == maKho && t.MaSP == maSP);
            if (tk != null)
            {
                tk.SoLuongTon += soLuong;
                if (tk.SoLuongTon < tk.MucToiThieu) tk.TrangThaiCanhBao = "Dưới tối thiểu";
                else if (tk.SoLuongTon > tk.MucToiDa) tk.TrangThaiCanhBao = "Vượt tối đa";
                else tk.TrangThaiCanhBao = "Bình thường";
            }
            else
            {
                _db.TonKhos.Add(new TonKho
                {
                    MaKho = maKho,
                    MaSP = maSP,
                    SoLuongTon = soLuong,
                    SoLuongDaBan = 0,
                    MucToiThieu = 20,
                    MucToiDa = 200,
                    TenKho = kho?.TenKho,
                    TenSP = sp?.TenSP,
                    DonViTinh = sp?.DonViTinh,
                    TrangThaiCanhBao = "Bình thường"
                });
            }

            _db.PhieuNhaps.Insert(0, phieuNhap);
            _db.ChiTietPhieuNhaps.AddRange(phieuNhap.ChiTiet);

            ViewBag.Success = $"Nhập hàng thành công! Đã lưu phiếu '{newPnId}' trị giá {thanhTien:N0} VNĐ và cập nhật số lượng tồn kho +{soLuong}.";
            ViewBag.RecentImports = _db.PhieuNhaps.OrderByDescending(p => p.NgayNhap).Take(5).ToList();
            return View("NhapHang");
        }

        // ==========================================
        // 8. UC8: QUẢN LÝ TỒN KHO & CẢNH BÁO
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult TonKho(string? maKho, string? filterCanhBao)
        {
            ViewBag.Khos = _db.Khos.ToList();
            ViewBag.SelectedKho = maKho;
            ViewBag.SelectedFilter = filterCanhBao;

            var list = _db.TonKhos.AsEnumerable();

            if (!string.IsNullOrEmpty(maKho))
            {
                list = list.Where(t => t.MaKho == maKho);
            }

            if (!string.IsNullOrEmpty(filterCanhBao))
            {
                if (filterCanhBao == "DuoiToiThieu")
                    list = list.Where(t => t.SoLuongTon < t.MucToiThieu);
                else if (filterCanhBao == "VuotToiDa")
                    list = list.Where(t => t.SoLuongTon > t.MucToiDa);
                else if (filterCanhBao == "BinhThuong")
                    list = list.Where(t => t.SoLuongTon >= t.MucToiThieu && t.SoLuongTon <= t.MucToiDa);
            }

            // Đồng bộ trạng thái cảnh báo
            var res = list.Select(t =>
            {
                if (t.SoLuongTon < t.MucToiThieu) t.TrangThaiCanhBao = "Dưới mức tối thiểu";
                else if (t.SoLuongTon > t.MucToiDa) t.TrangThaiCanhBao = "Vượt mức tối đa";
                else t.TrangThaiCanhBao = "Bình thường";
                return t;
            }).ToList();

            ViewBag.TongSoLuongTon = _db.TonKhos.Sum(t => t.SoLuongTon);
            ViewBag.TongSoLuongDaBan = _db.TonKhos.Sum(t => t.SoLuongDaBan);
            ViewBag.SoCanhBaoDuoi = _db.TonKhos.Count(t => t.SoLuongTon < t.MucToiThieu);
            ViewBag.SoCanhBaoVuot = _db.TonKhos.Count(t => t.SoLuongTon > t.MucToiDa);

            return View(res);
        }

        // ==========================================
        // 9. UC9: QUẢN LÝ CÔNG NỢ (KHÁCH HÀNG & NHÀ CUNG CẤP)
        // ==========================================
        [Authorize(Roles = "Admin")]
        public IActionResult CongNo(string? tab, string? maDoiTuong, string? trangThai)
        {
            tab = string.IsNullOrEmpty(tab) ? "KhachHang" : tab;
            ViewBag.CurrentTab = tab;
            ViewBag.KhachHangs = _db.KhachHangs.ToList();
            ViewBag.NhaCungCaps = _db.NhaCungCaps.ToList();
            ViewBag.SelectedDoiTuong = maDoiTuong;
            ViewBag.SelectedTrangThai = trangThai;

            var list = _db.CongNos.AsEnumerable();

            if (tab == "KhachHang")
            {
                list = list.Where(c => c.LoaiCongNo == "Phải thu");
                if (!string.IsNullOrEmpty(maDoiTuong))
                    list = list.Where(c => c.MaKhachHang == maDoiTuong);
            }
            else
            {
                list = list.Where(c => c.LoaiCongNo == "Phải trả");
                if (!string.IsNullOrEmpty(maDoiTuong))
                    list = list.Where(c => c.MaNCC == maDoiTuong);
            }

            if (trangThai == "Quá hạn")
            {
                list = list.Where(c => c.SoTienNo > 0 && c.TrangThai != "Đã thanh toán" &&
                    (c.TrangThai == "Quá hạn" ||
                     (c.HanThanhToan.HasValue && c.HanThanhToan.Value.Date < DateTime.Today)));
            }
            else if (trangThai == "Chưa trả")
            {
                list = list.Where(c => c.SoTienNo > 0 && c.TrangThai != "Đã thanh toán" &&
                    c.TrangThai != "Quá hạn" &&
                    (!c.HanThanhToan.HasValue || c.HanThanhToan.Value.Date >= DateTime.Today));
            }
            else if (trangThai == "Đã thanh toán")
            {
                list = list.Where(c => c.SoTienNo <= 0 || c.TrangThai == "Đã thanh toán");
            }

            var result = list.ToList();

            // Tính toán tổng số
            ViewBag.TongNo = result.Sum(c => c.SoTienNo);
            ViewBag.SoChungTuQuaHan = result.Count(c => c.SoTienNo > 0 &&
                c.TrangThai != "Đã thanh toán" &&
                (c.TrangThai == "Quá hạn" || (c.HanThanhToan.HasValue && c.HanThanhToan.Value.Date < DateTime.Today)));

            return View(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatCongNo(string maCongNo, decimal soTienThanhToan)
        {
            var cn = _db.CongNos.FirstOrDefault(c => c.MaCongNo == maCongNo);
            if (cn == null)
            {
                TempData["Error"] = "Chứng từ công nợ không tồn tại!";
                return RedirectToAction("CongNo");
            }

            if (soTienThanhToan <= 0 || soTienThanhToan > cn.SoTienNo)
            {
                TempData["Error"] = $"Số tiền thanh toán không hợp lệ (Phải > 0 và <= số nợ {cn.SoTienNo:N0} đ)!";
                return RedirectToAction("CongNo", new { tab = cn.LoaiCongNo == "Phải thu" ? "KhachHang" : "NhaCungCap" });
            }

            var update = await _sqlDataStore.RecordDebtPaymentAsync(maCongNo, soTienThanhToan);
            if (update == null)
            {
                TempData["Error"] = "Chứng từ đã thay đổi hoặc đã được thanh toán. Vui lòng tải lại danh sách công nợ.";
                return RedirectToAction("CongNo", new { tab = cn.LoaiCongNo == "Phải thu" ? "KhachHang" : "NhaCungCap" });
            }

            cn.SoTienNo = update.Value.Remaining;
            cn.TrangThai = update.Value.Status;
            if (cn.SoTienNo == 0)
            {
                cn.CanhBao = "Đã tất toán";
            }
            else
            {
                cn.CanhBao = cn.HanThanhToan.HasValue && cn.HanThanhToan.Value.Date < DateTime.Today
                    ? "Quá hạn thanh toán"
                    : "Đang trả góp";
            }

            TempData["Success"] = $"Cập nhật công nợ chứng từ '{maCongNo}' thành công! Đã thanh toán {soTienThanhToan:N0} VNĐ. Dư nợ còn lại: {cn.SoTienNo:N0} VNĐ.";
            return RedirectToAction("CongNo", new { tab = cn.LoaiCongNo == "Phải thu" ? "KhachHang" : "NhaCungCap" });
        }
    }
}
