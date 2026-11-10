using Microsoft.AspNetCore.Mvc;
using EMarket.Services;
using EMarket.Models;
using Microsoft.AspNetCore.Authorization;

namespace EMarket.Controllers
{
    [Authorize]
    public class UsecaseController : Controller
    {
        private readonly DataStore _db;

        public UsecaseController(DataStore db)
        {
            _db = db;
        }

        // ==========================================
        // 1. UC1: THEO DÕI QUÁ TRÌNH XỬ LÝ ĐƠN HÀNG
        // ==========================================
        [Authorize(Roles = "Customer")]
        public IActionResult TheoDoiDonHang(string? maDonHang)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            ViewBag.AllOrders = _db.DonHangs
                .Where(d => d.MaKhachHang == customerId)
                .OrderByDescending(d => d.NgayDat)
                .ToList();
            if (string.IsNullOrWhiteSpace(maDonHang))
            {
                return View(null);
            }

            var order = _db.DonHangs.FirstOrDefault(d =>
                d.MaKhachHang == customerId &&
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
            var customerId = User.FindFirst("CustomerId")?.Value;
            ViewBag.KhachHangs = _db.KhachHangs.Where(k => k.MaKhachHang == customerId).ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();
            return View();
        }

        [Authorize(Roles = "Customer")]
        [HttpPost]
        public IActionResult DatHang(string maKhachHang, string maSP, int soLuong, string? ghiChu)
        {
            maKhachHang = User.FindFirst("CustomerId")?.Value ?? string.Empty;
            ViewBag.KhachHangs = _db.KhachHangs.Where(k => k.MaKhachHang == maKhachHang).ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();

            if (string.IsNullOrEmpty(maKhachHang) || string.IsNullOrEmpty(maSP) || soLuong <= 0)
            {
                ViewBag.Error = "Thông tin không hợp lệ! Vui lòng chọn khách hàng, mặt hàng và nhập số lượng > 0.";
                return View();
            }

            var sp = _db.SanPhams.FirstOrDefault(s => s.MaSP == maSP);
            if (sp == null)
            {
                ViewBag.Error = "Sản phẩm không tồn tại trong hệ thống!";
                return View();
            }

            if (sp.SoLuongTon < soLuong)
            {
                ViewBag.Error = $"Hàng không đủ! Sản phẩm '{sp.TenSP}' chỉ còn {sp.SoLuongTon} {sp.DonViTinh} trong kho (bạn đặt {soLuong}).";
                return View();
            }

            var kh = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == maKhachHang);
            var newId = "DH" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var thanhTien = soLuong * sp.GiaBan;

            var donHang = new DonHang
            {
                MaDonHang = newId,
                MaKhachHang = maKhachHang,
                MaNV = kh?.MaNV ?? "NV02",
                NgayDat = DateTime.Now,
                TrangThai = "Đang xử lý",
                TongTien = thanhTien,
                TenKhachHang = kh?.HoTen,
                SoDienThoai = kh?.SoDienThoai,
                ChiTiet = new List<ChiTietDonHang>
                {
                    new ChiTietDonHang
                    {
                        MaDonHang = newId,
                        MaSP = sp.MaSP,
                        TenSP = sp.TenSP,
                        SoLuong = soLuong,
                        DonGia = sp.GiaBan,
                        ThanhTien = thanhTien,
                        DonViTinh = sp.DonViTinh
                    }
                }
            };

            // Trừ tồn kho
            sp.SoLuongTon -= soLuong;
            var tk = _db.TonKhos.FirstOrDefault(t => t.MaSP == sp.MaSP);
            if (tk != null)
            {
                tk.SoLuongTon -= soLuong;
                tk.SoLuongDaBan += soLuong;
            }

            _db.DonHangs.Insert(0, donHang);
            _db.ChiTietDonHangs.AddRange(donHang.ChiTiet);

            ViewBag.Success = $"Đặt hàng thành công! Mã đơn hàng của bạn là: {newId}. Bạn có thể theo dõi tiến độ ngay bây giờ.";
            ViewBag.NewOrder = donHang;
            return View();
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
        [HttpPost]
        public IActionResult LapDonBanHang(string maKhachHang, string maSP, int soLuong, decimal? giaApDung)
        {
            ViewBag.KhachHangs = _db.KhachHangs.ToList();
            ViewBag.SanPhams = _db.SanPhams.ToList();
            ViewBag.RecentOrders = _db.DonHangs.OrderByDescending(d => d.NgayDat).Take(5).ToList();

            if (string.IsNullOrEmpty(maKhachHang) || string.IsNullOrEmpty(maSP) || soLuong <= 0)
            {
                ViewBag.Error = "Thông tin đơn hàng không hợp lệ! Vui lòng chọn khách hàng, hàng hóa và số lượng hợp lệ.";
                return View("BanHang");
            }

            var sp = _db.SanPhams.FirstOrDefault(s => s.MaSP == maSP);
            if (sp == null || sp.SoLuongTon < soLuong)
            {
                ViewBag.Error = $"Kho không đủ hàng! Tồn kho hiện tại: {sp?.SoLuongTon ?? 0}. Yêu cầu xuất: {soLuong}.";
                return View("BanHang");
            }

            var kh = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == maKhachHang);
            decimal donGia = giaApDung ?? sp.GiaBan;
            decimal thanhTien = soLuong * donGia;

            var newId = "DH" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var donHang = new DonHang
            {
                MaDonHang = newId,
                MaKhachHang = maKhachHang,
                MaNV = "NV02", // NV Bán hàng
                NgayDat = DateTime.Now,
                TrangThai = "Đã hoàn tất",
                TongTien = thanhTien,
                TenKhachHang = kh?.HoTen,
                SoDienThoai = kh?.SoDienThoai,
                ChiTiet = new List<ChiTietDonHang>
                {
                    new ChiTietDonHang
                    {
                        MaDonHang = newId,
                        MaSP = sp.MaSP,
                        TenSP = sp.TenSP,
                        SoLuong = soLuong,
                        DonGia = donGia,
                        ThanhTien = thanhTien,
                        DonViTinh = sp.DonViTinh
                    }
                }
            };

            // Cập nhật tồn kho & đã bán
            sp.SoLuongTon -= soLuong;
            var tk = _db.TonKhos.FirstOrDefault(t => t.MaSP == sp.MaSP);
            if (tk != null)
            {
                tk.SoLuongTon -= soLuong;
                tk.SoLuongDaBan += soLuong;
            }

            // Tích điểm 1% cho khách hàng
            int diemCong = (int)(thanhTien / 10000);
            if (kh != null)
            {
                kh.DiemTichLuy += diemCong;
                var the = _db.TheThanhViens.FirstOrDefault(t => t.MaKhachHang == kh.MaKhachHang);
                if (the != null) the.DiemTichLuy += diemCong;
            }

            _db.DonHangs.Insert(0, donHang);
            _db.ChiTietDonHangs.AddRange(donHang.ChiTiet);

            ViewBag.Success = $"Lập và lưu đơn hàng thành công! Mã hóa đơn: {newId}. Cập nhật kho -{soLuong} {sp.DonViTinh}, cộng +{diemCong} điểm cho khách hàng.";
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
        public IActionResult ThemKhachHang(KhachHang kh)
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
            kh.TenLoai = loai?.TenLoai ?? "Khách thông thường";
            kh.DiemTichLuy = 0;
            _db.KhachHangs.Add(kh);

            // Tự động tạo Thẻ thành viên
            _db.TheThanhViens.Add(new TheThanhVien
            {
                MaThe = "THE" + kh.MaKhachHang,
                MaKhachHang = kh.MaKhachHang,
                NgayLap = DateTime.Now,
                DiemTichLuy = 0
            });

            TempData["Success"] = $"Thêm khách hàng '{kh.HoTen}' ({kh.MaKhachHang}) thành công!";
            return RedirectToAction("KhachHang");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult SuaKhachHang(KhachHang kh)
        {
            var existing = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == kh.MaKhachHang);
            if (existing == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng cần cập nhật!";
                return RedirectToAction("KhachHang");
            }

            existing.HoTen = kh.HoTen;
            existing.SoDienThoai = kh.SoDienThoai;
            existing.Email = kh.Email;
            existing.DiaChi = kh.DiaChi;
            existing.MaLoai = kh.MaLoai;
            existing.MaNV = kh.MaNV;
            var loai = _db.LoaiKhachHangs.FirstOrDefault(l => l.MaLoai == kh.MaLoai);
            existing.TenLoai = loai?.TenLoai ?? existing.TenLoai;

            TempData["Success"] = $"Cập nhật khách hàng '{existing.HoTen}' thành công!";
            return RedirectToAction("KhachHang");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult XoaKhachHang(string maKhachHang)
        {
            var kh = _db.KhachHangs.FirstOrDefault(k => k.MaKhachHang == maKhachHang);
            if (kh == null)
            {
                TempData["Error"] = "Không tìm thấy khách hàng cần xóa!";
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
            return View(kh);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult QuyDoiDiem(string maKhachHang, int soDiemDoi)
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

            if (soDiemDoi <= 0)
            {
                ViewBag.Error = "Số điểm quy đổi phải lớn hơn 0!";
                return View("DiemTichLuy", kh);
            }

            if (kh.DiemTichLuy < soDiemDoi)
            {
                ViewBag.Error = $"Không đủ điểm! Khách hàng chỉ có {kh.DiemTichLuy} điểm nhưng yêu cầu đổi {soDiemDoi} điểm.";
                return View("DiemTichLuy", kh);
            }

            kh.DiemTichLuy -= soDiemDoi;
            var the = _db.TheThanhViens.FirstOrDefault(t => t.MaKhachHang == kh.MaKhachHang);
            if (the != null) the.DiemTichLuy = kh.DiemTichLuy;

            decimal giaTriVoucher = soDiemDoi * 1000;
            ViewBag.Success = $"Quy đổi thành công {soDiemDoi} điểm! Khách hàng nhận Voucher giảm giá {giaTriVoucher:N0} VNĐ. Điểm còn lại: {kh.DiemTichLuy}.";
            return View("DiemTichLuy", kh);
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
        public IActionResult TaoPhieuNhap(string maNCC, string maKho, string maSP, int soLuong, decimal donGiaNhap)
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

            var newPnId = "PN" + DateTime.Now.ToString("yyyyMMddHHmmss");
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

            if (!string.IsNullOrEmpty(trangThai))
            {
                list = list.Where(c => c.TrangThai == trangThai);
            }

            var result = list.ToList();

            // Tính toán tổng số
            ViewBag.TongNo = result.Sum(c => c.SoTienNo);
            ViewBag.SoChungTuQuaHan = result.Count(c => c.TrangThai == "Quá hạn" || (c.HanThanhToan.HasValue && c.HanThanhToan.Value < DateTime.Now));

            return View(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult CapNhatCongNo(string maCongNo, decimal soTienThanhToan)
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

            cn.SoTienNo -= soTienThanhToan;
            if (cn.SoTienNo == 0)
            {
                cn.TrangThai = "Đã thanh toán";
                cn.CanhBao = "Đã tất toán";
            }
            else
            {
                cn.CanhBao = "Đang trả góp";
            }

            TempData["Success"] = $"Cập nhật công nợ chứng từ '{maCongNo}' thành công! Đã thanh toán {soTienThanhToan:N0} VNĐ. Dư nợ còn lại: {cn.SoTienNo:N0} VNĐ.";
            return RedirectToAction("CongNo", new { tab = cn.LoaiCongNo == "Phải thu" ? "KhachHang" : "NhaCungCap" });
        }
    }
}
