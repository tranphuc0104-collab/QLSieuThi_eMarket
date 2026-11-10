using EMarket.Models;

namespace EMarket.Services
{
    public class DataStore
    {
        public List<LoaiKhachHang> LoaiKhachHangs { get; set; } = new();
        public List<NhanVien> NhanViens { get; set; } = new();
        public List<KhachHang> KhachHangs { get; set; } = new();
        public List<TheThanhVien> TheThanhViens { get; set; } = new();
        public List<DanhMuc> DanhMucs { get; set; } = new();
        public List<NhaCungCap> NhaCungCaps { get; set; } = new();
        public List<Kho> Khos { get; set; } = new();
        public List<SanPham> SanPhams { get; set; } = new();
        public List<DonHang> DonHangs { get; set; } = new();
        public List<ChiTietDonHang> ChiTietDonHangs { get; set; } = new();
        public List<PhieuNhap> PhieuNhaps { get; set; } = new();
        public List<ChiTietPhieuNhap> ChiTietPhieuNhaps { get; set; } = new();
        public List<TonKho> TonKhos { get; set; } = new();
        public List<CongNo> CongNos { get; set; } = new();

        public DataStore()
        {
            SeedData();
        }

        private void SeedData()
        {
            // 1. Loại khách hàng
            LoaiKhachHangs.AddRange(new[]
            {
                new LoaiKhachHang { MaLoai = "LKH01", TenLoai = "Khách hàng thông thường", MoTa = "Khách mua lẻ vãng lai, tích điểm tiêu chuẩn 1%" },
                new LoaiKhachHang { MaLoai = "LKH02", TenLoai = "Khách hàng Thân Thiết", MoTa = "Khách hàng tích lũy trên 500 điểm, chiết khấu 5%" },
                new LoaiKhachHang { MaLoai = "LKH03", TenLoai = "Khách hàng VIP", MoTa = "Đại lý và khách hàng doanh nghiệp, hưởng bảng giá riêng chiết khấu 10%" }
            });

            // 2. Nhân viên
            NhanViens.AddRange(new[]
            {
                new NhanVien { MaNV = "NV01", HoTen = "Trần Bảo Phúc", SoDienThoai = "0901234567", Email = "phuc.tran@emarket.vn", TenDangNhap = "phuc.tran", MatKhau = "123456", VaiTro = "Quản lý siêu thị" },
                new NhanVien { MaNV = "NV02", HoTen = "Hoàng Ngọc Hà", SoDienThoai = "0912345678", Email = "ha.hoang@emarket.vn", TenDangNhap = "ha.hoang", MatKhau = "123456", VaiTro = "Nhân viên bán hàng" },
                new NhanVien { MaNV = "NV03", HoTen = "Văn Trần Tuấn Khải", SoDienThoai = "0987654321", Email = "khai.van@emarket.vn", TenDangNhap = "khai.van", MatKhau = "123456", VaiTro = "Nhân viên quản lý kho" },
                new NhanVien { MaNV = "NV04", HoTen = "Lê Thị Thảo", SoDienThoai = "0934567890", Email = "thao.le@emarket.vn", TenDangNhap = "thao.le", MatKhau = "123456", VaiTro = "Chăm sóc khách hàng" },
                new NhanVien { MaNV = "NV05", HoTen = "Nguyễn Văn Hải", SoDienThoai = "0945678901", Email = "hai.nguyen@emarket.vn", TenDangNhap = "hai.nguyen", MatKhau = "123456", VaiTro = "Nhân viên quản lý công nợ" }
            });

            // 3. Khách hàng
            KhachHangs.AddRange(new[]
            {
                new KhachHang { MaKhachHang = "KH01", MaLoai = "LKH03", MaNV = "NV04", HoTen = "Công Ty TNHH Thực Phẩm Xanh Á Châu", SoDienThoai = "02743828999", Email = "contact@greenfood.vn", DiaChi = "Số 18, Đại lộ Bình Dương, TP. Thủ Dầu Một", TenLoai = "Khách hàng VIP", DiemTichLuy = 1250 },
                new KhachHang { MaKhachHang = "KH02", MaLoai = "LKH02", MaNV = "NV04", HoTen = "Nguyễn Thị Mai Hoa", SoDienThoai = "0978112233", Email = "maihoa.nguyen@gmail.com", DiaChi = "45 Lê Hồng Phong, P. Phú Hòa, TP. TDM", TenLoai = "Khách hàng Thân Thiết", DiemTichLuy = 620 },
                new KhachHang { MaKhachHang = "KH03", MaLoai = "LKH01", MaNV = "NV04", HoTen = "Phạm Hoàng Long", SoDienThoai = "0982334455", Email = "longph@hotmail.com", DiaChi = "12 Chánh Nghĩa, TP. Thủ Dầu Một", TenLoai = "Khách hàng thông thường", DiemTichLuy = 85 },
                new KhachHang { MaKhachHang = "KH04", MaLoai = "LKH02", MaNV = "NV04", HoTen = "Trần Thị Ánh Tuyết", SoDienThoai = "0918776655", Email = "anhtuyet@yahoo.com", DiaChi = "88 CMT8, Lái Thiêu, TP. Thuận An", TenLoai = "Khách hàng Thân Thiết", DiemTichLuy = 430 }
            });

            // 4. Thẻ thành viên
            TheThanhViens.AddRange(new[]
            {
                new TheThanhVien { MaThe = "THE001", MaKhachHang = "KH01", NgayLap = DateTime.Now.AddYears(-2), DiemTichLuy = 1250 },
                new TheThanhVien { MaThe = "THE002", MaKhachHang = "KH02", NgayLap = DateTime.Now.AddMonths(-10), DiemTichLuy = 620 },
                new TheThanhVien { MaThe = "THE003", MaKhachHang = "KH03", NgayLap = DateTime.Now.AddMonths(-3), DiemTichLuy = 85 },
                new TheThanhVien { MaThe = "THE004", MaKhachHang = "KH04", NgayLap = DateTime.Now.AddMonths(-6), DiemTichLuy = 430 }
            });

            // 5. Danh mục
            DanhMucs.AddRange(new[]
            {
                new DanhMuc { MaDanhMuc = "DM01", TenDanhMuc = "Thực phẩm tươi sống", MoTa = "Rau củ quả tươi, thịt gia súc gia cầm tiêu chuẩn VietGAP" },
                new DanhMuc { MaDanhMuc = "DM02", TenDanhMuc = "Đồ uống & Giải khát", MoTa = "Nước ngọt, nước khoáng, sữa tươi thanh trùng, bia" },
                new DanhMuc { MaDanhMuc = "DM03", TenDanhMuc = "Bánh kẹo & Đồ khô", MoTa = "Bánh ngọt, bánh quy, ngũ cốc dinh dưỡng, gia vị khô" },
                new DanhMuc { MaDanhMuc = "DM04", TenDanhMuc = "Hóa mỹ phẩm & Tẩy rửa", MoTa = "Nước giặt, xả vải, sữa tắm dầu gội, nước lau sàn" }
            });

            // 6. Nhà cung cấp
            NhaCungCaps.AddRange(new[]
            {
                new NhaCungCap { MaNCC = "NCC01", TenNCC = "Công ty CP Sữa Việt Nam (Vinamilk)", DiaChi = "10 Tân Trào, P. Tân Phú, Quận 7, TP. HCM", SoDienThoai = "02854155555", Email = "vinamilk@vinamilk.com.vn" },
                new NhaCungCap { MaNCC = "NCC02", TenNCC = "Tập đoàn Masan Consumer", DiaChi = "Tầng 12 Tòa nhà MPlaza, Q.1, TP. HCM", SoDienThoai = "02862555660", Email = "contact@masangroup.com" },
                new NhaCungCap { MaNCC = "NCC03", TenNCC = "Công ty CP Nông Sản Sạch Đà Lạt Farm", DiaChi = "Km 19, Đèo Prenn, TP. Đà Lạt, Lâm Đồng", SoDienThoai = "02633889900", Email = "sales@dalatfarm.vn" },
                new NhaCungCap { MaNCC = "NCC04", TenNCC = "Công ty Unilever Việt Nam", DiaChi = "Lô A2-3 KCN Tây Bắc Củ Chi, TP. HCM", SoDienThoai = "02838236655", Email = "info@unilever.com.vn" }
            });

            // 7. Kho
            Khos.AddRange(new[]
            {
                new Kho { MaKho = "KHO01", TenKho = "Kho Trung Tâm Siêu Thị (Kho Tổng)", DiaChi = "Khuôn viên EMarket - Tầng trệt, TP. Thủ Dầu Một" },
                new Kho { MaKho = "KHO02", TenKho = "Kho Lạnh Thực Phẩm Tươi Sống", DiaChi = "Khu B kho trung tâm (Nhiệt độ 2-5°C)" },
                new Kho { MaKho = "KHO03", TenKho = "Kho Hàng Khô & Hóa Phẩm", DiaChi = "Khu C tầng lửng siêu thị EMarket" }
            });

            // 8. Sản phẩm
            SanPhams.AddRange(new[]
            {
                new SanPham { MaSP = "SP01", MaDanhMuc = "DM02", TenSP = "Sữa Tươi Tiệt Trùng Vinamilk 100% 1L", MoTa = "Hộp giấy 1 lít có đường, giàu canxi và vitamin D3", GiaBan = 36000, HanSuDung = DateTime.Now.AddMonths(5), DonViTinh = "Hộp", TenDanhMuc = "Đồ uống & Giải khát", SoLuongTon = 180 },
                new SanPham { MaSP = "SP02", MaDanhMuc = "DM03", TenSP = "Mì Ăn Liền Omachi Sốt Bò Hầm", MoTa = "Thùng 30 gói x 80g, sợi mì khoai tây dinh dưỡng", GiaBan = 245000, HanSuDung = DateTime.Now.AddMonths(7), DonViTinh = "Thùng", TenDanhMuc = "Bánh kẹo & Đồ khô", SoLuongTon = 45 },
                new SanPham { MaSP = "SP03", MaDanhMuc = "DM01", TenSP = "Thịt Ba Chỉ Heo VietGAP Sạch", MoTa = "Khay hút chân không 500g tiêu chuẩn an toàn sinh học", GiaBan = 75000, HanSuDung = DateTime.Now.AddDays(4), DonViTinh = "Khay", TenDanhMuc = "Thực phẩm tươi sống", SoLuongTon = 8 }, // Gần dưới tối thiểu!
                new SanPham { MaSP = "SP04", MaDanhMuc = "DM04", TenSP = "Nước Giặt OMO Matic Cửa Trên 3.6kg", MoTa = "Túi 3.6kg hương thơm dịu mát bền lâu", GiaBan = 185000, HanSuDung = DateTime.Now.AddYears(2), DonViTinh = "Túi", TenDanhMuc = "Hóa mỹ phẩm & Tẩy rửa", SoLuongTon = 620 }, // Vượt mức tối đa!
                new SanPham { MaSP = "SP05", MaDanhMuc = "DM01", TenSP = "Cà Chua Beef Đà Lạt Hữu Cơ", MoTa = "Hộp 1kg quả mọng, sạch không dư lượng thuốc BVTV", GiaBan = 42000, HanSuDung = DateTime.Now.AddDays(7), DonViTinh = "Hộp", TenDanhMuc = "Thực phẩm tươi sống", SoLuongTon = 35 },
                new SanPham { MaSP = "SP06", MaDanhMuc = "DM02", TenSP = "Nước Khoáng Tinh Khiết Lavie 500ml", MoTa = "Thùng 24 chai x 500ml khoáng thiên nhiên", GiaBan = 110000, HanSuDung = DateTime.Now.AddYears(1), DonViTinh = "Thùng", TenDanhMuc = "Đồ uống & Giải khát", SoLuongTon = 90 },
                new SanPham { MaSP = "SP07", MaDanhMuc = "DM03", TenSP = "Gạo Thơm Lài ST25 Hút Chân Không 5kg", MoTa = "Gạo ngon nhất thế giới, túi 5kg dẻo thơm hảo hạng", GiaBan = 175000, HanSuDung = DateTime.Now.AddMonths(11), DonViTinh = "Túi", TenDanhMuc = "Bánh kẹo & Đồ khô", SoLuongTon = 15 }
            });

            // 9. Tồn kho
            TonKhos.AddRange(new[]
            {
                new TonKho { MaKho = "KHO01", MaSP = "SP01", SoLuongTon = 180, SoLuongDaBan = 420, MucToiThieu = 50, MucToiDa = 300, TenKho = "Kho Tổng", TenSP = "Sữa Tươi Vinamilk 1L", DonViTinh = "Hộp", TrangThaiCanhBao = "Bình thường" },
                new TonKho { MaKho = "KHO01", MaSP = "SP02", SoLuongTon = 45, SoLuongDaBan = 190, MucToiThieu = 20, MucToiDa = 100, TenKho = "Kho Tổng", TenSP = "Mì Omachi Sốt Bò Hầm", DonViTinh = "Thùng", TrangThaiCanhBao = "Bình thường" },
                new TonKho { MaKho = "KHO02", MaSP = "SP03", SoLuongTon = 8, SoLuongDaBan = 85, MucToiThieu = 20, MucToiDa = 100, TenKho = "Kho Lạnh", TenSP = "Thịt Ba Chỉ Heo VietGAP", DonViTinh = "Khay", TrangThaiCanhBao = "Dưới tối thiểu" },
                new TonKho { MaKho = "KHO03", MaSP = "SP04", SoLuongTon = 620, SoLuongDaBan = 310, MucToiThieu = 50, MucToiDa = 400, TenKho = "Kho Hóa Phẩm", TenSP = "Nước Giặt OMO Matic 3.6kg", DonViTinh = "Túi", TrangThaiCanhBao = "Vượt tối đa" },
                new TonKho { MaKho = "KHO02", MaSP = "SP05", SoLuongTon = 35, SoLuongDaBan = 120, MucToiThieu = 15, MucToiDa = 80, TenKho = "Kho Lạnh", TenSP = "Cà Chua Beef Đà Lạt", DonViTinh = "Hộp", TrangThaiCanhBao = "Bình thường" },
                new TonKho { MaKho = "KHO01", MaSP = "SP06", SoLuongTon = 90, SoLuongDaBan = 300, MucToiThieu = 30, MucToiDa = 200, TenKho = "Kho Tổng", TenSP = "Nước Khoáng Lavie 500ml", DonViTinh = "Thùng", TrangThaiCanhBao = "Bình thường" },
                new TonKho { MaKho = "KHO03", MaSP = "SP07", SoLuongTon = 15, SoLuongDaBan = 95, MucToiThieu = 20, MucToiDa = 150, TenKho = "Kho Hóa Phẩm", TenSP = "Gạo Thơm Lài ST25 5kg", DonViTinh = "Túi", TrangThaiCanhBao = "Dưới tối thiểu" }
            });

            // 10. Đơn hàng
            var dh1 = new DonHang
            {
                MaDonHang = "DH20261101",
                MaKhachHang = "KH01",
                MaNV = "NV02",
                NgayDat = DateTime.Now.AddDays(-2),
                TrangThai = "Đã hoàn tất",
                TongTien = 12500000,
                TenKhachHang = "Công Ty TNHH Thực Phẩm Xanh Á Châu",
                SoDienThoai = "02743828999",
                ChiTiet = new List<ChiTietDonHang>
                {
                    new ChiTietDonHang { MaDonHang = "DH20261101", MaSP = "SP01", TenSP = "Sữa Tươi Vinamilk 1L", SoLuong = 100, DonGia = 34000, ThanhTien = 3400000, DonViTinh = "Hộp" },
                    new ChiTietDonHang { MaDonHang = "DH20261101", MaSP = "SP02", TenSP = "Mì Ăn Liền Omachi Sốt Bò Hầm", SoLuong = 30, DonGia = 230000, ThanhTien = 6900000, DonViTinh = "Thùng" },
                    new ChiTietDonHang { MaDonHang = "DH20261101", MaSP = "SP06", TenSP = "Nước Khoáng Tinh Khiết Lavie 500ml", SoLuong = 20, DonGia = 110000, ThanhTien = 2200000, DonViTinh = "Thùng" }
                }
            };

            var dh2 = new DonHang
            {
                MaDonHang = "DH20261102",
                MaKhachHang = "KH02",
                MaNV = "NV02",
                NgayDat = DateTime.Now.AddHours(-5),
                TrangThai = "Đang xử lý",
                TongTien = 620000,
                TenKhachHang = "Nguyễn Thị Mai Hoa",
                SoDienThoai = "0978112233",
                ChiTiet = new List<ChiTietDonHang>
                {
                    new ChiTietDonHang { MaDonHang = "DH20261102", MaSP = "SP03", TenSP = "Thịt Ba Chỉ Heo VietGAP Sạch", SoLuong = 4, DonGia = 75000, ThanhTien = 300000, DonViTinh = "Khay" },
                    new ChiTietDonHang { MaDonHang = "DH20261102", MaSP = "SP05", TenSP = "Cà Chua Beef Đà Lạt Hữu Cơ", SoLuong = 4, DonGia = 42000, ThanhTien = 168000, DonViTinh = "Hộp" },
                    new ChiTietDonHang { MaDonHang = "DH20261102", MaSP = "SP07", TenSP = "Gạo Thơm Lài ST25 Hút Chân Không 5kg", SoLuong = 1, DonGia = 175000, ThanhTien = 175000, DonViTinh = "Túi" }
                }
            };

            var dh3 = new DonHang
            {
                MaDonHang = "DH20261103",
                MaKhachHang = "KH03",
                MaNV = "NV02",
                NgayDat = DateTime.Now.AddMinutes(-30),
                TrangThai = "Đang xử lý",
                TongTien = 221000,
                TenKhachHang = "Phạm Hoàng Long",
                SoDienThoai = "0982334455",
                ChiTiet = new List<ChiTietDonHang>
                {
                    new ChiTietDonHang { MaDonHang = "DH20261103", MaSP = "SP01", TenSP = "Sữa Tươi Vinamilk 1L", SoLuong = 1, DonGia = 36000, ThanhTien = 36000, DonViTinh = "Hộp" },
                    new ChiTietDonHang { MaDonHang = "DH20261103", MaSP = "SP04", TenSP = "Nước Giặt OMO Matic 3.6kg", SoLuong = 1, DonGia = 185000, ThanhTien = 185000, DonViTinh = "Túi" }
                }
            };

            DonHangs.Add(dh1);
            DonHangs.Add(dh2);
            DonHangs.Add(dh3);
            ChiTietDonHangs.AddRange(dh1.ChiTiet);
            ChiTietDonHangs.AddRange(dh2.ChiTiet);
            ChiTietDonHangs.AddRange(dh3.ChiTiet);

            // 11. Phiếu nhập
            var pn1 = new PhieuNhap
            {
                MaPhieuNhap = "PN20261101",
                MaNCC = "NCC01",
                MaKho = "KHO01",
                MaNV = "NV03",
                NgayNhap = DateTime.Now.AddDays(-10),
                TongTien = 14400000,
                TenNCC = "Công ty CP Sữa Việt Nam (Vinamilk)",
                TenKho = "Kho Trung Tâm Siêu Thị",
                ChiTiet = new List<ChiTietPhieuNhap>
                {
                    new ChiTietPhieuNhap { MaPhieuNhap = "PN20261101", MaSP = "SP01", TenSP = "Sữa Tươi Tiệt Trùng Vinamilk 100% 1L", SoLuong = 500, DonGiaNhap = 28800, ThanhTien = 14400000 }
                }
            };

            var pn2 = new PhieuNhap
            {
                MaPhieuNhap = "PN20261102",
                MaNCC = "NCC02",
                MaKho = "KHO01",
                MaNV = "NV03",
                NgayNhap = DateTime.Now.AddDays(-5),
                TongTien = 20500000,
                TenNCC = "Tập đoàn Masan Consumer",
                TenKho = "Kho Trung Tâm Siêu Thị",
                ChiTiet = new List<ChiTietPhieuNhap>
                {
                    new ChiTietPhieuNhap { MaPhieuNhap = "PN20261102", MaSP = "SP02", TenSP = "Mì Ăn Liền Omachi Sốt Bò Hầm", SoLuong = 100, DonGiaNhap = 205000, ThanhTien = 20500000 }
                }
            };

            PhieuNhaps.Add(pn1);
            PhieuNhaps.Add(pn2);
            ChiTietPhieuNhaps.AddRange(pn1.ChiTiet);
            ChiTietPhieuNhaps.AddRange(pn2.ChiTiet);

            // 12. Công nợ (Khách hàng & NCC)
            CongNos.AddRange(new[]
            {
                // Nợ khách hàng (Phải thu)
                new CongNo { MaCongNo = "CN-KH01", MaNV = "NV05", MaKhachHang = "KH01", LoaiCongNo = "Phải thu", NgayPhatSinh = DateTime.Now.AddDays(-45), SoTienNo = 45000000, HanThanhToan = DateTime.Now.AddDays(-15), TrangThai = "Quá hạn", DoiTuongTen = "Công Ty TNHH Thực Phẩm Xanh Á Châu", CanhBao = "Quá hạn thanh toán" },
                new CongNo { MaCongNo = "CN-KH02", MaNV = "NV05", MaKhachHang = "KH01", LoaiCongNo = "Phải thu", NgayPhatSinh = DateTime.Now.AddDays(-5), SoTienNo = 82000000, HanThanhToan = DateTime.Now.AddDays(25), TrangThai = "Chưa trả", DoiTuongTen = "Công Ty TNHH Thực Phẩm Xanh Á Châu", CanhBao = "Vượt mức nợ tối đa (Hạn mức 50tr)" },
                new CongNo { MaCongNo = "CN-KH03", MaNV = "NV05", MaKhachHang = "KH02", LoaiCongNo = "Phải thu", NgayPhatSinh = DateTime.Now.AddDays(-2), SoTienNo = 3200000, HanThanhToan = DateTime.Now.AddDays(15), TrangThai = "Chưa trả", DoiTuongTen = "Nguyễn Thị Mai Hoa", CanhBao = "Bình thường" },
                
                // Nợ nhà cung cấp (Phải trả)
                new CongNo { MaCongNo = "CN-NCC01", MaNV = "NV05", MaNCC = "NCC01", LoaiCongNo = "Phải trả", NgayPhatSinh = DateTime.Now.AddDays(-20), SoTienNo = 28800000, HanThanhToan = DateTime.Now.AddDays(10), TrangThai = "Chưa trả", DoiTuongTen = "Công ty CP Sữa Việt Nam (Vinamilk)", CanhBao = "Bình thường" },
                new CongNo { MaCongNo = "CN-NCC02", MaNV = "NV05", MaNCC = "NCC02", LoaiCongNo = "Phải trả", NgayPhatSinh = DateTime.Now.AddDays(-60), SoTienNo = 54000000, HanThanhToan = DateTime.Now.AddDays(-10), TrangThai = "Quá hạn", DoiTuongTen = "Tập đoàn Masan Consumer", CanhBao = "Quá hạn thanh toán" }
            });
        }
    }
}
