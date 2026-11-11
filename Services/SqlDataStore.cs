using System.Data;
using EMarket.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace EMarket.Services;

public class SqlDataStore
{
    private readonly string _connectionString;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;

    public SqlDataStore(IConfiguration configuration, IPasswordHasher<UserAccount> passwordHasher)
    {
        _connectionString = configuration.GetConnectionString("EMarket")
            ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:EMarket.");
        _passwordHasher = passwordHasher;
    }

    public async Task InitializeAsync(DataStore store)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await EnsureAccountTableAsync(connection);
        await EnsureOrderCheckoutColumnsAsync(connection);
        await EnsureDebtOrderReferenceColumnAsync(connection);
        await EnsureVoucherTableAsync(connection);
        await EnsureCartTableAsync(connection);

        if (!await HasBusinessRowsAsync(connection))
        {
            await SeedDemoDataAsync(connection, store);
        }

        await ReconcileOrderTotalsAsync(connection);
        await MigrateLegacyEmployeePasswordsAsync(connection);
        await LoadAsync(connection, store);
    }

    private static async Task EnsureOrderCheckoutColumnsAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            IF COL_LENGTH(N'dbo.DonHang', N'TenNguoiNhan') IS NULL
                ALTER TABLE dbo.DonHang ADD TenNguoiNhan NVARCHAR(100) NULL;
            IF COL_LENGTH(N'dbo.DonHang', N'SoDienThoaiGiaoHang') IS NULL
                ALTER TABLE dbo.DonHang ADD SoDienThoaiGiaoHang VARCHAR(20) NULL;
            IF COL_LENGTH(N'dbo.DonHang', N'DiaChiGiaoHang') IS NULL
                ALTER TABLE dbo.DonHang ADD DiaChiGiaoHang NVARCHAR(255) NULL;
            IF COL_LENGTH(N'dbo.DonHang', N'PhuongThucThanhToan') IS NULL
                ALTER TABLE dbo.DonHang ADD PhuongThucThanhToan NVARCHAR(50) NULL;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureDebtOrderReferenceColumnAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            IF COL_LENGTH(N'dbo.CongNo', N'MaDonHang') IS NULL
                ALTER TABLE dbo.CongNo ADD MaDonHang VARCHAR(50) NULL;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureVoucherTableAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            IF COL_LENGTH(N'dbo.DonHang', N'MaVoucher') IS NULL
                ALTER TABLE dbo.DonHang ADD MaVoucher VARCHAR(50) NULL;
            IF COL_LENGTH(N'dbo.DonHang', N'TienGiamGia') IS NULL
                ALTER TABLE dbo.DonHang ADD TienGiamGia DECIMAL(18, 2) NOT NULL
                    CONSTRAINT DF_DonHang_TienGiamGia DEFAULT (0);
            IF OBJECT_ID(N'dbo.Voucher', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Voucher
                (
                    MaVoucher VARCHAR(50) NOT NULL PRIMARY KEY,
                    MaKhachHang VARCHAR(50) NOT NULL,
                    GiaTri DECIMAL(18, 2) NOT NULL,
                    NgayTao DATE NOT NULL,
                    NgayHetHan DATE NOT NULL,
                    MaDonHangSuDung VARCHAR(50) NULL,
                    CONSTRAINT FK_Voucher_KhachHang FOREIGN KEY (MaKhachHang)
                        REFERENCES dbo.KhachHang(MaKhachHang),
                    CONSTRAINT CK_Voucher_GiaTri CHECK (GiaTri > 0)
                );
            END;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureCartTableAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            IF OBJECT_ID(N'dbo.GioHang', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.GioHang
                (
                    MaPhien VARCHAR(50) NOT NULL,
                    MaSP VARCHAR(50) NOT NULL,
                    SoLuong INT NOT NULL,
                    CapNhatLuc DATETIME2 NOT NULL CONSTRAINT DF_GioHang_CapNhatLuc DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_GioHang PRIMARY KEY (MaPhien, MaSP),
                    CONSTRAINT FK_GioHang_SanPham FOREIGN KEY (MaSP) REFERENCES dbo.SanPham(MaSP),
                    CONSTRAINT CK_GioHang_SoLuong CHECK (SoLuong BETWEEN 1 AND 9999)
                );
            END;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ReconcileOrderTotalsAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            UPDATE dh
            SET TongTien = totals.TongTienHang - COALESCE(dh.TienGiamGia, 0)
            FROM dbo.DonHang dh
            CROSS APPLY (
                SELECT SUM(ct.ThanhTien) AS TongTienHang
                FROM dbo.ChiTietDonHang ct
                WHERE ct.MaDonHang = dh.MaDonHang
            ) totals
            WHERE totals.TongTienHang IS NOT NULL
              AND dh.TongTien <> totals.TongTienHang - COALESCE(dh.TienGiamGia, 0);
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string?> SaveCustomerOrderAsync(DonHang order, string cartSessionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        foreach (var line in order.ChiTiet)
        {
            await using var productCommand = new SqlCommand(
                "SELECT GiaBan FROM dbo.SanPham WITH (UPDLOCK, HOLDLOCK) WHERE MaSP = @MaSP;",
                connection, transaction);
            productCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
            var price = await productCommand.ExecuteScalarAsync();
            if (price == null || price == DBNull.Value)
            {
                await transaction.RollbackAsync();
                return $"Sản phẩm '{line.MaSP}' không còn tồn tại. Vui lòng cập nhật giỏ hàng.";
            }

            line.DonGia = Convert.ToDecimal(price);
            await using var stockTotalCommand = new SqlCommand(
                """
                SELECT COALESCE(SUM(SoLuongTon), 0)
                FROM dbo.TonKho WITH (UPDLOCK, HOLDLOCK)
                WHERE MaSP = @MaSP;
                """, connection, transaction);
            stockTotalCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
            var availableStock = Convert.ToInt32(await stockTotalCommand.ExecuteScalarAsync());
            if (line.SoLuong > availableStock)
            {
                await transaction.RollbackAsync();
                return $"Sản phẩm '{line.TenSP}' chỉ còn {availableStock} trong kho. Vui lòng điều chỉnh giỏ hàng.";
            }
            line.ThanhTien = line.SoLuong * line.DonGia;
        }

        order.TongTien = order.ChiTiet.Sum(line => line.ThanhTien);
        order.TienGiamGia = 0;
        if (!string.IsNullOrWhiteSpace(order.MaVoucher))
        {
            await using var voucherCommand = new SqlCommand(
                """
                SELECT GiaTri
                FROM dbo.Voucher WITH (UPDLOCK, HOLDLOCK)
                WHERE MaVoucher = @MaVoucher
                  AND MaKhachHang = @MaKhachHang
                  AND MaDonHangSuDung IS NULL
                  AND NgayHetHan >= @Ngay;
                """, connection, transaction);
            voucherCommand.Parameters.Add("@MaVoucher", SqlDbType.VarChar, 50).Value = order.MaVoucher.Trim();
            voucherCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            voucherCommand.Parameters.Add("@Ngay", SqlDbType.Date).Value = DateTime.Today;
            var voucherValue = await voucherCommand.ExecuteScalarAsync();
            if (voucherValue == null || voucherValue == DBNull.Value)
            {
                await transaction.RollbackAsync();
                return "Voucher không hợp lệ, đã dùng, hết hạn hoặc không thuộc tài khoản của bạn.";
            }

            order.TienGiamGia = Math.Min(Convert.ToDecimal(voucherValue), order.TongTien);
            order.TongTien -= order.TienGiamGia;
        }

        await using (var orderCommand = new SqlCommand(
            """
            INSERT INTO dbo.DonHang
                (MaDonHang, MaKhachHang, MaNV, NgayDat, TrangThai, TongTien,
                 TenNguoiNhan, SoDienThoaiGiaoHang, DiaChiGiaoHang, PhuongThucThanhToan,
                 MaVoucher, TienGiamGia)
            VALUES
                (@MaDonHang, @MaKhachHang, @MaNV, @NgayDat, @TrangThai, @TongTien,
                 @TenNguoiNhan, @SoDienThoaiGiaoHang, @DiaChiGiaoHang, @PhuongThucThanhToan,
                 @MaVoucher, @TienGiamGia);
            """, connection, transaction))
        {
            orderCommand.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = order.MaDonHang;
            orderCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            orderCommand.Parameters.Add("@MaNV", SqlDbType.VarChar, 50).Value = (object?)order.MaNV ?? DBNull.Value;
            orderCommand.Parameters.Add("@NgayDat", SqlDbType.DateTime).Value = order.NgayDat!.Value;
            orderCommand.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = order.TrangThai!;
            var totalParameter = orderCommand.Parameters.Add("@TongTien", SqlDbType.Decimal);
            totalParameter.Precision = 18;
            totalParameter.Scale = 2;
            totalParameter.Value = order.TongTien;
            orderCommand.Parameters.Add("@TenNguoiNhan", SqlDbType.NVarChar, 100).Value = order.TenNguoiNhan!;
            orderCommand.Parameters.Add("@SoDienThoaiGiaoHang", SqlDbType.VarChar, 20).Value = order.SoDienThoaiGiaoHang!;
            orderCommand.Parameters.Add("@DiaChiGiaoHang", SqlDbType.NVarChar, 255).Value = order.DiaChiGiaoHang!;
            orderCommand.Parameters.Add("@PhuongThucThanhToan", SqlDbType.NVarChar, 50).Value = order.PhuongThucThanhToan!;
            orderCommand.Parameters.Add("@MaVoucher", SqlDbType.VarChar, 50).Value =
                string.IsNullOrWhiteSpace(order.MaVoucher) ? DBNull.Value : order.MaVoucher.Trim();
            var discountParameter = orderCommand.Parameters.Add("@TienGiamGia", SqlDbType.Decimal);
            discountParameter.Precision = 18;
            discountParameter.Scale = 2;
            discountParameter.Value = order.TienGiamGia;
            await orderCommand.ExecuteNonQueryAsync();
        }

        foreach (var line in order.ChiTiet)
        {
            await using (var detailCommand = new SqlCommand(
                """
                INSERT INTO dbo.ChiTietDonHang (MaDonHang, MaSP, SoLuong, DonGia, ThanhTien)
                VALUES (@MaDonHang, @MaSP, @SoLuong, @DonGia, @ThanhTien);
                """, connection, transaction))
            {
                detailCommand.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = order.MaDonHang;
                detailCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
                detailCommand.Parameters.Add("@SoLuong", SqlDbType.Int).Value = line.SoLuong;
                var priceParameter = detailCommand.Parameters.Add("@DonGia", SqlDbType.Decimal);
                priceParameter.Precision = 18;
                priceParameter.Scale = 2;
                priceParameter.Value = line.DonGia;
                var lineTotalParameter = detailCommand.Parameters.Add("@ThanhTien", SqlDbType.Decimal);
                lineTotalParameter.Precision = 18;
                lineTotalParameter.Scale = 2;
                lineTotalParameter.Value = line.ThanhTien;
                await detailCommand.ExecuteNonQueryAsync();
            }

            var remainingQuantity = line.SoLuong;
            var warehouseStocks = new List<(string WarehouseId, int Quantity)>();
            await using (var stockCommand = new SqlCommand(
                """
                SELECT MaKho, SoLuongTon
                FROM dbo.TonKho WITH (UPDLOCK, HOLDLOCK)
                WHERE MaSP = @MaSP AND SoLuongTon > 0
                ORDER BY MaKho;
                """, connection, transaction))
            {
                stockCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
                await using var reader = await stockCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    warehouseStocks.Add((reader.GetString(0), reader.GetInt32(1)));
                }
            }

            foreach (var stock in warehouseStocks)
            {
                var deduction = Math.Min(remainingQuantity, stock.Quantity);
                await using var updateStock = new SqlCommand(
                    """
                    UPDATE dbo.TonKho
                    SET SoLuongTon = SoLuongTon - @SoLuong, SoLuongDaBan = SoLuongDaBan + @SoLuong
                    WHERE MaKho = @MaKho AND MaSP = @MaSP AND SoLuongTon >= @SoLuong;
                    """, connection, transaction);
                updateStock.Parameters.Add("@SoLuong", SqlDbType.Int).Value = deduction;
                updateStock.Parameters.Add("@MaKho", SqlDbType.VarChar, 50).Value = stock.WarehouseId;
                updateStock.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
                if (await updateStock.ExecuteNonQueryAsync() != 1)
                {
                    await transaction.RollbackAsync();
                    return $"Kho vừa thay đổi số lượng của '{line.TenSP}'. Vui lòng kiểm tra lại giỏ hàng.";
                }

                remainingQuantity -= deduction;
                if (remainingQuantity == 0)
                {
                    break;
                }
            }

            if (remainingQuantity > 0)
            {
                await transaction.RollbackAsync();
                return $"Kho không đủ số lượng '{line.TenSP}'. Vui lòng điều chỉnh giỏ hàng.";
            }
        }

        if (!string.IsNullOrWhiteSpace(order.MaVoucher))
        {
            await using var useVoucherCommand = new SqlCommand(
                """
                UPDATE dbo.Voucher
                SET MaDonHangSuDung = @MaDonHang
                WHERE MaVoucher = @MaVoucher
                  AND MaKhachHang = @MaKhachHang
                  AND MaDonHangSuDung IS NULL
                  AND NgayHetHan >= @Ngay;
                """, connection, transaction);
            useVoucherCommand.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = order.MaDonHang;
            useVoucherCommand.Parameters.Add("@MaVoucher", SqlDbType.VarChar, 50).Value = order.MaVoucher.Trim();
            useVoucherCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            useVoucherCommand.Parameters.Add("@Ngay", SqlDbType.Date).Value = DateTime.Today;
            if (await useVoucherCommand.ExecuteNonQueryAsync() != 1)
            {
                await transaction.RollbackAsync();
                return "Voucher vừa được sử dụng hoặc không còn hợp lệ. Vui lòng thử lại.";
            }
        }

        if (order.PhuongThucThanhToan == "Ghi nợ")
        {
            await using var debtCommand = new SqlCommand(
                """
                INSERT INTO dbo.CongNo
                    (MaCongNo, MaNV, MaKhachHang, MaDonHang, LoaiCongNo, NgayPhatSinh,
                     SoTienNo, HanThanhToan, TrangThai)
                VALUES
                    (@MaCongNo, @MaNV, @MaKhachHang, @MaDonHang, N'Phải thu', @NgayPhatSinh,
                     @SoTienNo, @HanThanhToan, N'Chưa trả');
                """, connection, transaction);
            debtCommand.Parameters.Add("@MaCongNo", SqlDbType.VarChar, 50).Value = "CN-" + order.MaDonHang;
            debtCommand.Parameters.Add("@MaNV", SqlDbType.VarChar, 50).Value = (object?)order.MaNVQuanLyCongNo ?? DBNull.Value;
            debtCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            debtCommand.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = order.MaDonHang;
            debtCommand.Parameters.Add("@NgayPhatSinh", SqlDbType.Date).Value = order.NgayDat!.Value.Date;
            var debtAmount = debtCommand.Parameters.Add("@SoTienNo", SqlDbType.Decimal);
            debtAmount.Precision = 18;
            debtAmount.Scale = 2;
            debtAmount.Value = order.TongTien;
            debtCommand.Parameters.Add("@HanThanhToan", SqlDbType.Date).Value = order.NgayDat.Value.Date.AddDays(30);
            await debtCommand.ExecuteNonQueryAsync();
        }

        var pointsEarned = (int)(order.TongTien / 10000);
        await using (var pointsCommand = new SqlCommand(
            """
            UPDATE dbo.TheThanhVien
            SET DiemTichLuy = DiemTichLuy + @Diem
            WHERE MaKhachHang = @MaKhachHang;
            """, connection, transaction))
        {
            pointsCommand.Parameters.Add("@Diem", SqlDbType.Int).Value = pointsEarned;
            pointsCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            await pointsCommand.ExecuteNonQueryAsync();
        }

        await using (var clearCartCommand = new SqlCommand(
            "DELETE FROM dbo.GioHang WHERE MaPhien = @MaPhien;",
            connection, transaction))
        {
            clearCartCommand.Parameters.Add("@MaPhien", SqlDbType.VarChar, 50).Value = cartSessionId;
            await clearCartCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return null;
    }

    public async Task<Dictionary<string, int>> GetCartAsync(string cartSessionId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT MaSP, SoLuong FROM dbo.GioHang WHERE MaPhien = @MaPhien;",
            connection);
        command.Parameters.Add("@MaPhien", SqlDbType.VarChar, 50).Value = cartSessionId;
        var items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(reader.GetString(0), reader.GetInt32(1));
        }

        return items;
    }

    public async Task<string?> GetCustomerIdForAccountAsync(string userName)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            SELECT MaKhachHang
            FROM dbo.TaiKhoan
            WHERE TenDangNhap = @TenDangNhap AND VaiTro = 'Customer';
            """, connection);
        command.Parameters.Add("@TenDangNhap", SqlDbType.VarChar, 50).Value = userName;
        var result = await command.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    public async Task<KhachHang?> GetCustomerAsync(string customerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            SELECT kh.MaKhachHang, kh.MaLoai, kh.MaNV, kh.HoTen, kh.SoDienThoai, kh.Email, kh.DiaChi,
                   lk.TenLoai, COALESCE(tt.DiemTichLuy, 0) AS DiemTichLuy
            FROM dbo.KhachHang kh
            LEFT JOIN dbo.LoaiKhachHang lk ON lk.MaLoai = kh.MaLoai
            LEFT JOIN dbo.TheThanhVien tt ON tt.MaKhachHang = kh.MaKhachHang
            WHERE kh.MaKhachHang = @MaKhachHang;
            """, connection);
        command.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new KhachHang
        {
            MaKhachHang = Text(reader, "MaKhachHang") ?? string.Empty,
            MaLoai = Text(reader, "MaLoai"),
            MaNV = Text(reader, "MaNV"),
            HoTen = Text(reader, "HoTen") ?? string.Empty,
            SoDienThoai = Text(reader, "SoDienThoai"),
            Email = Text(reader, "Email"),
            DiaChi = Text(reader, "DiaChi"),
            TenLoai = Text(reader, "TenLoai"),
            DiemTichLuy = Number(reader, "DiemTichLuy")
        };
    }

    public async Task<List<DonHang>> GetCustomerOrdersAsync(string customerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            SELECT dh.MaDonHang, dh.MaKhachHang, dh.MaNV, dh.NgayDat, dh.TrangThai, dh.TongTien,
                   COALESCE(dh.TenNguoiNhan, kh.HoTen) AS TenKhachHang,
                   dh.TenNguoiNhan, COALESCE(dh.SoDienThoaiGiaoHang, kh.SoDienThoai) AS SoDienThoai,
                   dh.SoDienThoaiGiaoHang, dh.DiaChiGiaoHang, dh.PhuongThucThanhToan,
                   dh.MaVoucher, dh.TienGiamGia,
                   ct.MaSP, ct.SoLuong, ct.DonGia, ct.ThanhTien, sp.TenSP, sp.DonViTinh
            FROM dbo.DonHang dh
            LEFT JOIN dbo.KhachHang kh ON kh.MaKhachHang = dh.MaKhachHang
            LEFT JOIN dbo.ChiTietDonHang ct ON ct.MaDonHang = dh.MaDonHang
            LEFT JOIN dbo.SanPham sp ON sp.MaSP = ct.MaSP
            WHERE dh.MaKhachHang = @MaKhachHang
            ORDER BY dh.NgayDat DESC, dh.MaDonHang, ct.MaSP;
            """, connection);
        command.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
        var ordersById = new Dictionary<string, DonHang>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var orderId = Text(reader, "MaDonHang") ?? string.Empty;
            if (!ordersById.TryGetValue(orderId, out var order))
            {
                order = new DonHang
                {
                    MaDonHang = orderId,
                    MaKhachHang = Text(reader, "MaKhachHang"),
                    MaNV = Text(reader, "MaNV"),
                    NgayDat = Date(reader, "NgayDat"),
                    TrangThai = Text(reader, "TrangThai"),
                    TongTien = Amount(reader, "TongTien"),
                    TenKhachHang = Text(reader, "TenKhachHang"),
                    TenNguoiNhan = Text(reader, "TenNguoiNhan"),
                    SoDienThoai = Text(reader, "SoDienThoai"),
                    SoDienThoaiGiaoHang = Text(reader, "SoDienThoaiGiaoHang"),
                    DiaChiGiaoHang = Text(reader, "DiaChiGiaoHang"),
                    PhuongThucThanhToan = Text(reader, "PhuongThucThanhToan"),
                    MaVoucher = Text(reader, "MaVoucher"),
                    TienGiamGia = Amount(reader, "TienGiamGia")
                };
                ordersById.Add(orderId, order);
            }

            var productId = Text(reader, "MaSP");
            if (productId != null)
            {
                order.ChiTiet.Add(new ChiTietDonHang
                {
                    MaDonHang = orderId,
                    MaSP = productId,
                    SoLuong = Number(reader, "SoLuong"),
                    DonGia = Amount(reader, "DonGia"),
                    ThanhTien = Amount(reader, "ThanhTien"),
                    TenSP = Text(reader, "TenSP"),
                    DonViTinh = Text(reader, "DonViTinh")
                });
            }
        }

        return ordersById.Values.ToList();
    }

    public async Task<List<Voucher>> GetCustomerVouchersAsync(string customerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return await ReadAsync(connection,
            """
            SELECT MaVoucher, MaKhachHang, GiaTri, NgayTao, NgayHetHan, MaDonHangSuDung
            FROM dbo.Voucher
            WHERE MaKhachHang = @MaKhachHang
            ORDER BY NgayTao DESC;
            """, command => command.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId,
            reader => new Voucher
            {
                MaVoucher = Text(reader, "MaVoucher") ?? string.Empty,
                MaKhachHang = Text(reader, "MaKhachHang") ?? string.Empty,
                GiaTri = Amount(reader, "GiaTri"),
                NgayTao = Date(reader, "NgayTao") ?? DateTime.MinValue,
                NgayHetHan = Date(reader, "NgayHetHan") ?? DateTime.MinValue,
                MaDonHangSuDung = Text(reader, "MaDonHangSuDung")
            });
    }

    public async Task<TheThanhVien?> GetMemberCardAsync(string customerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            SELECT MaThe, MaKhachHang, NgayLap, DiemTichLuy
            FROM dbo.TheThanhVien
            WHERE MaKhachHang = @MaKhachHang;
            """, connection);
        command.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new TheThanhVien
        {
            MaThe = Text(reader, "MaThe") ?? string.Empty,
            MaKhachHang = Text(reader, "MaKhachHang"),
            NgayLap = Date(reader, "NgayLap"),
            DiemTichLuy = Number(reader, "DiemTichLuy")
        };
    }

    public async Task SaveCartAsync(string cartSessionId, IReadOnlyDictionary<string, int> items)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        await using (var lockCommand = new SqlCommand(
            "SELECT MaSP FROM dbo.GioHang WITH (UPDLOCK, HOLDLOCK) WHERE MaPhien = @MaPhien;",
            connection, transaction))
        {
            lockCommand.Parameters.Add("@MaPhien", SqlDbType.VarChar, 50).Value = cartSessionId;
            await lockCommand.ExecuteNonQueryAsync();
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 50 || item.Value is < 1 or > 9999)
            {
                await transaction.RollbackAsync();
                throw new ArgumentException("Giỏ hàng chứa mã sản phẩm hoặc số lượng không hợp lệ.", nameof(items));
            }

            await using var productCommand = new SqlCommand(
                "SELECT 1 FROM dbo.SanPham WHERE MaSP = @MaSP;",
                connection, transaction);
            productCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = item.Key;
            if (await productCommand.ExecuteScalarAsync() == null)
            {
                await transaction.RollbackAsync();
                throw new ArgumentException($"Sản phẩm '{item.Key}' không tồn tại.", nameof(items));
            }
        }

        await using (var clearCommand = new SqlCommand(
            "DELETE FROM dbo.GioHang WHERE MaPhien = @MaPhien;",
            connection, transaction))
        {
            clearCommand.Parameters.Add("@MaPhien", SqlDbType.VarChar, 50).Value = cartSessionId;
            await clearCommand.ExecuteNonQueryAsync();
        }

        foreach (var item in items)
        {
            await using var insertCommand = new SqlCommand(
                "INSERT INTO dbo.GioHang (MaPhien, MaSP, SoLuong) VALUES (@MaPhien, @MaSP, @SoLuong);",
                connection, transaction);
            insertCommand.Parameters.Add("@MaPhien", SqlDbType.VarChar, 50).Value = cartSessionId;
            insertCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = item.Key;
            insertCommand.Parameters.Add("@SoLuong", SqlDbType.Int).Value = item.Value;
            await insertCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<int?> RedeemVoucherAsync(string customerId, int points, Voucher voucher)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        int remainingPoints;
        await using (var pointsCommand = new SqlCommand(
            """
            UPDATE dbo.TheThanhVien
            SET DiemTichLuy = DiemTichLuy - @Diem
            OUTPUT INSERTED.DiemTichLuy
            WHERE MaKhachHang = @MaKhachHang AND DiemTichLuy >= @Diem;
            """, connection, transaction))
        {
            pointsCommand.Parameters.Add("@Diem", SqlDbType.Int).Value = points;
            pointsCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
            var result = await pointsCommand.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
            {
                await transaction.RollbackAsync();
                return null;
            }

            remainingPoints = Convert.ToInt32(result);
        }

        await using (var voucherCommand = new SqlCommand(
            """
            INSERT INTO dbo.Voucher (MaVoucher, MaKhachHang, GiaTri, NgayTao, NgayHetHan)
            VALUES (@MaVoucher, @MaKhachHang, @GiaTri, @NgayTao, @NgayHetHan);
            """, connection, transaction))
        {
            voucherCommand.Parameters.Add("@MaVoucher", SqlDbType.VarChar, 50).Value = voucher.MaVoucher;
            voucherCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
            var valueParameter = voucherCommand.Parameters.Add("@GiaTri", SqlDbType.Decimal);
            valueParameter.Precision = 18;
            valueParameter.Scale = 2;
            valueParameter.Value = voucher.GiaTri;
            voucherCommand.Parameters.Add("@NgayTao", SqlDbType.Date).Value = voucher.NgayTao;
            voucherCommand.Parameters.Add("@NgayHetHan", SqlDbType.Date).Value = voucher.NgayHetHan;
            await voucherCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return remainingPoints;
    }

    public async Task<(decimal Remaining, string Status)?> RecordDebtPaymentAsync(string debtId, decimal amount)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            UPDATE dbo.CongNo
            SET SoTienNo = SoTienNo - @SoTien,
                TrangThai = CASE WHEN SoTienNo - @SoTien = 0 THEN N'Đã thanh toán' ELSE N'Chưa trả' END
            OUTPUT INSERTED.SoTienNo, INSERTED.TrangThai
            WHERE MaCongNo = @MaCongNo AND SoTienNo > 0 AND SoTienNo >= @SoTien;
            """, connection);
        command.Parameters.Add("@MaCongNo", SqlDbType.VarChar, 50).Value = debtId;
        var amountParameter = command.Parameters.Add("@SoTien", SqlDbType.Decimal);
        amountParameter.Precision = 18;
        amountParameter.Scale = 2;
        amountParameter.Value = amount;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetDecimal(0), reader.GetString(1));
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, string currentStatus, string nextStatus)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            UPDATE dbo.DonHang
            SET TrangThai = @TrangThai
            WHERE MaDonHang = @MaDonHang
              AND TrangThai = @TrangThaiHienTai;
            """, connection);
        command.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = nextStatus;
        command.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = orderId;
        command.Parameters.Add("@TrangThaiHienTai", SqlDbType.NVarChar, 50).Value = currentStatus;
        return await command.ExecuteNonQueryAsync() == 1;
    }

    public async Task<string?> SavePosOrderAsync(DonHang order)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        foreach (var line in order.ChiTiet)
        {
            var remaining = line.SoLuong;
            var stockRows = new List<(string WarehouseId, int Quantity)>();
            await using (var stockCommand = new SqlCommand(
                """
                SELECT MaKho, SoLuongTon
                FROM dbo.TonKho WITH (UPDLOCK, HOLDLOCK)
                WHERE MaSP = @MaSP AND SoLuongTon > 0
                ORDER BY MaKho;
                """, connection, transaction))
            {
                stockCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
                await using var reader = await stockCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    stockRows.Add((reader.GetString(0), reader.GetInt32(1)));
                }
            }

            if (stockRows.Sum(row => row.Quantity) < line.SoLuong)
            {
                await transaction.RollbackAsync();
                return $"Kho vừa thay đổi số lượng của '{line.TenSP}'. Vui lòng tải lại và lập hóa đơn lại.";
            }

            foreach (var stock in stockRows)
            {
                var deduction = Math.Min(remaining, stock.Quantity);
                await using var updateStock = new SqlCommand(
                    """
                    UPDATE dbo.TonKho
                    SET SoLuongTon = SoLuongTon - @SoLuong, SoLuongDaBan = SoLuongDaBan + @SoLuong
                    WHERE MaKho = @MaKho AND MaSP = @MaSP AND SoLuongTon >= @SoLuong;
                    """, connection, transaction);
                updateStock.Parameters.Add("@SoLuong", SqlDbType.Int).Value = deduction;
                updateStock.Parameters.Add("@MaKho", SqlDbType.VarChar, 50).Value = stock.WarehouseId;
                updateStock.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
                if (await updateStock.ExecuteNonQueryAsync() != 1)
                {
                    await transaction.RollbackAsync();
                    return $"Tồn kho của '{line.TenSP}' vừa thay đổi. Vui lòng thử lại.";
                }

                remaining -= deduction;
                if (remaining == 0) break;
            }
        }

        await using (var orderCommand = new SqlCommand(
            """
            INSERT INTO dbo.DonHang (MaDonHang, MaKhachHang, MaNV, NgayDat, TrangThai, TongTien)
            VALUES (@MaDonHang, @MaKhachHang, @MaNV, @NgayDat, @TrangThai, @TongTien);
            """, connection, transaction))
        {
            orderCommand.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = order.MaDonHang;
            orderCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            orderCommand.Parameters.Add("@MaNV", SqlDbType.VarChar, 50).Value = order.MaNV!;
            orderCommand.Parameters.Add("@NgayDat", SqlDbType.DateTime).Value = order.NgayDat!.Value;
            orderCommand.Parameters.Add("@TrangThai", SqlDbType.NVarChar, 50).Value = order.TrangThai!;
            var total = orderCommand.Parameters.Add("@TongTien", SqlDbType.Decimal);
            total.Precision = 18;
            total.Scale = 2;
            total.Value = order.TongTien;
            await orderCommand.ExecuteNonQueryAsync();
        }

        foreach (var line in order.ChiTiet)
        {
            await using var detailCommand = new SqlCommand(
                """
                INSERT INTO dbo.ChiTietDonHang (MaDonHang, MaSP, SoLuong, DonGia, ThanhTien)
                VALUES (@MaDonHang, @MaSP, @SoLuong, @DonGia, @ThanhTien);
                """, connection, transaction);
            detailCommand.Parameters.Add("@MaDonHang", SqlDbType.VarChar, 50).Value = order.MaDonHang;
            detailCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
            detailCommand.Parameters.Add("@SoLuong", SqlDbType.Int).Value = line.SoLuong;
            var price = detailCommand.Parameters.Add("@DonGia", SqlDbType.Decimal);
            price.Precision = 18;
            price.Scale = 2;
            price.Value = line.DonGia;
            var lineTotal = detailCommand.Parameters.Add("@ThanhTien", SqlDbType.Decimal);
            lineTotal.Precision = 18;
            lineTotal.Scale = 2;
            lineTotal.Value = line.ThanhTien;
            await detailCommand.ExecuteNonQueryAsync();
        }

        var points = (int)(order.TongTien / 10000);
        await using (var pointsCommand = new SqlCommand(
            """
            UPDATE dbo.TheThanhVien
            SET DiemTichLuy = DiemTichLuy + @Diem
            WHERE MaKhachHang = @MaKhachHang;
            """, connection, transaction))
        {
            pointsCommand.Parameters.Add("@Diem", SqlDbType.Int).Value = points;
            pointsCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = order.MaKhachHang!;
            await pointsCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return null;
    }

    public async Task<string?> SaveImportAsync(PhieuNhap import)
    {
        var line = import.ChiTiet.Single();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        await using (var importCommand = new SqlCommand(
            """
            INSERT INTO dbo.PhieuNhap (MaPhieuNhap, MaNCC, MaKho, MaNV, NgayNhap, TongTien)
            VALUES (@MaPhieuNhap, @MaNCC, @MaKho, @MaNV, @NgayNhap, @TongTien);
            INSERT INTO dbo.ChiTietPhieuNhap (MaPhieuNhap, MaSP, SoLuong, DonGiaNhap, ThanhTien)
            VALUES (@MaPhieuNhap, @MaSP, @SoLuong, @DonGiaNhap, @ThanhTien);
            """, connection, transaction))
        {
            importCommand.Parameters.Add("@MaPhieuNhap", SqlDbType.VarChar, 50).Value = import.MaPhieuNhap;
            importCommand.Parameters.Add("@MaNCC", SqlDbType.VarChar, 50).Value = import.MaNCC!;
            importCommand.Parameters.Add("@MaKho", SqlDbType.VarChar, 50).Value = import.MaKho!;
            importCommand.Parameters.Add("@MaNV", SqlDbType.VarChar, 50).Value = import.MaNV!;
            importCommand.Parameters.Add("@NgayNhap", SqlDbType.DateTime).Value = import.NgayNhap!.Value;
            var total = importCommand.Parameters.Add("@TongTien", SqlDbType.Decimal);
            total.Precision = 18;
            total.Scale = 2;
            total.Value = import.TongTien;
            importCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
            importCommand.Parameters.Add("@SoLuong", SqlDbType.Int).Value = line.SoLuong;
            var unitPrice = importCommand.Parameters.Add("@DonGiaNhap", SqlDbType.Decimal);
            unitPrice.Precision = 18;
            unitPrice.Scale = 2;
            unitPrice.Value = line.DonGiaNhap;
            var lineTotal = importCommand.Parameters.Add("@ThanhTien", SqlDbType.Decimal);
            lineTotal.Precision = 18;
            lineTotal.Scale = 2;
            lineTotal.Value = line.ThanhTien;
            await importCommand.ExecuteNonQueryAsync();
        }

        await using (var inventoryCommand = new SqlCommand(
            """
            UPDATE dbo.TonKho WITH (UPDLOCK, SERIALIZABLE)
            SET SoLuongTon = SoLuongTon + @SoLuong
            WHERE MaKho = @MaKho AND MaSP = @MaSP;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.TonKho (MaKho, MaSP, SoLuongTon, SoLuongDaBan, MucToiThieu, MucToiDa)
                VALUES (@MaKho, @MaSP, @SoLuong, 0, 20, 200);
            """, connection, transaction))
        {
            inventoryCommand.Parameters.Add("@MaKho", SqlDbType.VarChar, 50).Value = import.MaKho!;
            inventoryCommand.Parameters.Add("@MaSP", SqlDbType.VarChar, 50).Value = line.MaSP;
            inventoryCommand.Parameters.Add("@SoLuong", SqlDbType.Int).Value = line.SoLuong;
            await inventoryCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return null;
    }

    public async Task<string?> AddCustomerAsync(KhachHang customer, TheThanhVien card)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var command = new SqlCommand(
            """
            INSERT INTO dbo.KhachHang (MaKhachHang, MaLoai, MaNV, HoTen, SoDienThoai, Email, DiaChi)
            VALUES (@MaKhachHang, @MaLoai, @MaNV, @HoTen, @SoDienThoai, @Email, @DiaChi);
            INSERT INTO dbo.TheThanhVien (MaThe, MaKhachHang, NgayLap, DiemTichLuy)
            VALUES (@MaThe, @MaKhachHang, @NgayLap, 0);
            """, connection, transaction);
        AddCustomerParameters(command, customer);
        command.Parameters.Add("@MaThe", SqlDbType.VarChar, 50).Value = card.MaThe;
        command.Parameters.Add("@NgayLap", SqlDbType.Date).Value = card.NgayLap ?? DateTime.Today;
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return null;
    }

    public async Task<bool> UpdateCustomerAsync(KhachHang customer)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            """
            UPDATE dbo.KhachHang
            SET MaLoai = @MaLoai, MaNV = @MaNV, HoTen = @HoTen,
                SoDienThoai = @SoDienThoai, Email = @Email, DiaChi = @DiaChi
            WHERE MaKhachHang = @MaKhachHang;
            """, connection);
        AddCustomerParameters(command, customer);
        return await command.ExecuteNonQueryAsync() == 1;
    }

    public async Task<bool> DeleteCustomerAsync(string customerId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        await using (var checkCommand = new SqlCommand(
            """
            IF EXISTS (SELECT 1 FROM dbo.DonHang WHERE MaKhachHang = @MaKhachHang)
               OR EXISTS (SELECT 1 FROM dbo.CongNo WHERE MaKhachHang = @MaKhachHang)
               OR EXISTS (SELECT 1 FROM dbo.Voucher WHERE MaKhachHang = @MaKhachHang)
                SELECT 1 ELSE SELECT 0;
            """, connection, transaction))
        {
            checkCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
            var hasHistory = await checkCommand.ExecuteScalarAsync();
            if (hasHistory != null && Convert.ToInt32(hasHistory) == 1)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        await using (var deleteCommand = new SqlCommand(
            """
            DELETE FROM dbo.TaiKhoan WHERE MaKhachHang = @MaKhachHang;
            DELETE FROM dbo.TheThanhVien WHERE MaKhachHang = @MaKhachHang;
            DELETE FROM dbo.KhachHang WHERE MaKhachHang = @MaKhachHang;
            """, connection, transaction))
        {
            deleteCommand.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customerId;
            await deleteCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return true;
    }

    private static void AddCustomerParameters(SqlCommand command, KhachHang customer)
    {
        command.Parameters.Add("@MaKhachHang", SqlDbType.VarChar, 50).Value = customer.MaKhachHang;
        command.Parameters.Add("@MaLoai", SqlDbType.VarChar, 50).Value = (object?)customer.MaLoai ?? DBNull.Value;
        command.Parameters.Add("@MaNV", SqlDbType.VarChar, 50).Value = (object?)customer.MaNV ?? DBNull.Value;
        command.Parameters.Add("@HoTen", SqlDbType.NVarChar, 100).Value = customer.HoTen;
        command.Parameters.Add("@SoDienThoai", SqlDbType.VarChar, 20).Value = (object?)customer.SoDienThoai ?? DBNull.Value;
        command.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (object?)customer.Email ?? DBNull.Value;
        command.Parameters.Add("@DiaChi", SqlDbType.NVarChar, 255).Value = (object?)customer.DiaChi ?? DBNull.Value;
    }

    private async Task MigrateLegacyEmployeePasswordsAsync(SqlConnection connection)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        var employees = new List<(string UserName, string Password, string EmployeeId, string FullName)>();
        await using (var command = new SqlCommand(
            """
            SELECT nv.TenDangNhap, nv.MatKhau, nv.MaNV, nv.HoTen
            FROM dbo.NhanVien nv WITH (UPDLOCK, HOLDLOCK)
            WHERE NULLIF(nv.TenDangNhap, '') IS NOT NULL
              AND nv.MatKhau IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.TaiKhoan a WITH (UPDLOCK, HOLDLOCK)
                  WHERE a.TenDangNhap = nv.TenDangNhap
              );
            """, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                employees.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
        }

        foreach (var employee in employees)
        {
            var account = new UserAccount
            {
                UserName = employee.UserName,
                PasswordHash = string.Empty,
                Role = "Admin",
                DisplayName = employee.FullName
            };
            account.PasswordHash = _passwordHasher.HashPassword(account, employee.Password);

            await using var command = new SqlCommand(
                """
                INSERT INTO dbo.TaiKhoan (TenDangNhap, MatKhauHash, VaiTro, MaNV)
                VALUES (@UserName, @PasswordHash, 'Admin', @EmployeeId);
                UPDATE dbo.NhanVien SET MatKhau = NULL WHERE MaNV = @EmployeeId;
                """, connection, transaction);
            command.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = employee.UserName;
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = account.PasswordHash;
            command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 50).Value = employee.EmployeeId;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task EnsureAccountTableAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            """
            IF OBJECT_ID(N'dbo.TaiKhoan', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TaiKhoan
                (
                    TenDangNhap VARCHAR(50) NOT NULL PRIMARY KEY,
                    MatKhauHash NVARCHAR(500) NOT NULL,
                    VaiTro VARCHAR(20) NOT NULL,
                    MaNV VARCHAR(50) NULL,
                    MaKhachHang VARCHAR(50) NULL,
                    CONSTRAINT FK_TaiKhoan_NhanVien FOREIGN KEY (MaNV) REFERENCES dbo.NhanVien(MaNV),
                    CONSTRAINT FK_TaiKhoan_KhachHang FOREIGN KEY (MaKhachHang) REFERENCES dbo.KhachHang(MaKhachHang),
                    CONSTRAINT CK_TaiKhoan_VaiTro CHECK (VaiTro IN ('Admin', 'Customer'))
                );
                CREATE UNIQUE INDEX UQ_TaiKhoan_MaNV ON dbo.TaiKhoan(MaNV) WHERE MaNV IS NOT NULL;
                CREATE UNIQUE INDEX UQ_TaiKhoan_MaKhachHang ON dbo.TaiKhoan(MaKhachHang) WHERE MaKhachHang IS NOT NULL;
            END;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> HasBusinessRowsAsync(SqlConnection connection)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.LoaiKhachHang UNION ALL
                SELECT 1 FROM dbo.NhanVien UNION ALL
                SELECT 1 FROM dbo.KhachHang UNION ALL
                SELECT 1 FROM dbo.TheThanhVien UNION ALL
                SELECT 1 FROM dbo.DanhMuc UNION ALL
                SELECT 1 FROM dbo.NhaCungCap UNION ALL
                SELECT 1 FROM dbo.Kho UNION ALL
                SELECT 1 FROM dbo.SanPham UNION ALL
                SELECT 1 FROM dbo.DonHang UNION ALL
                SELECT 1 FROM dbo.ChiTietDonHang UNION ALL
                SELECT 1 FROM dbo.PhieuNhap UNION ALL
                SELECT 1 FROM dbo.ChiTietPhieuNhap UNION ALL
                SELECT 1 FROM dbo.TonKho UNION ALL
                SELECT 1 FROM dbo.CongNo
            ) THEN 1 ELSE 0 END;
            """;
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync())! != 0;
    }

    private static async Task SeedDemoDataAsync(SqlConnection connection, DataStore store)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        await InsertAsync(connection, transaction, "LoaiKhachHang", Rows(store.LoaiKhachHangs,
            ("MaLoai", typeof(string), x => x.MaLoai),
            ("TenLoai", typeof(string), x => x.TenLoai),
            ("MoTa", typeof(string), x => x.MoTa)));
        await InsertAsync(connection, transaction, "NhanVien", Rows(store.NhanViens,
            ("MaNV", typeof(string), x => x.MaNV),
            ("HoTen", typeof(string), x => x.HoTen),
            ("SoDienThoai", typeof(string), x => x.SoDienThoai),
            ("Email", typeof(string), x => x.Email),
            ("TenDangNhap", typeof(string), x => x.TenDangNhap),
            ("MatKhau", typeof(string), x => x.MatKhau),
            ("VaiTro", typeof(string), x => x.VaiTro)));
        await InsertAsync(connection, transaction, "KhachHang", Rows(store.KhachHangs,
            ("MaKhachHang", typeof(string), x => x.MaKhachHang),
            ("MaLoai", typeof(string), x => x.MaLoai),
            ("MaNV", typeof(string), x => x.MaNV),
            ("HoTen", typeof(string), x => x.HoTen),
            ("SoDienThoai", typeof(string), x => x.SoDienThoai),
            ("Email", typeof(string), x => x.Email),
            ("DiaChi", typeof(string), x => x.DiaChi)));
        await InsertAsync(connection, transaction, "TheThanhVien", Rows(store.TheThanhViens,
            ("MaThe", typeof(string), x => x.MaThe),
            ("MaKhachHang", typeof(string), x => x.MaKhachHang),
            ("NgayLap", typeof(DateTime), x => x.NgayLap),
            ("DiemTichLuy", typeof(int), x => x.DiemTichLuy)));
        await InsertAsync(connection, transaction, "DanhMuc", Rows(store.DanhMucs,
            ("MaDanhMuc", typeof(string), x => x.MaDanhMuc),
            ("TenDanhMuc", typeof(string), x => x.TenDanhMuc),
            ("MoTa", typeof(string), x => x.MoTa)));
        await InsertAsync(connection, transaction, "NhaCungCap", Rows(store.NhaCungCaps,
            ("MaNCC", typeof(string), x => x.MaNCC),
            ("TenNCC", typeof(string), x => x.TenNCC),
            ("DiaChi", typeof(string), x => x.DiaChi),
            ("SoDienThoai", typeof(string), x => x.SoDienThoai),
            ("Email", typeof(string), x => x.Email)));
        await InsertAsync(connection, transaction, "Kho", Rows(store.Khos,
            ("MaKho", typeof(string), x => x.MaKho),
            ("TenKho", typeof(string), x => x.TenKho),
            ("DiaChi", typeof(string), x => x.DiaChi)));
        await InsertAsync(connection, transaction, "SanPham", Rows(store.SanPhams,
            ("MaSP", typeof(string), x => x.MaSP),
            ("MaDanhMuc", typeof(string), x => x.MaDanhMuc),
            ("TenSP", typeof(string), x => x.TenSP),
            ("MoTa", typeof(string), x => x.MoTa),
            ("GiaBan", typeof(decimal), x => x.GiaBan),
            ("HanSuDung", typeof(DateTime), x => x.HanSuDung),
            ("DonViTinh", typeof(string), x => x.DonViTinh)));
        await InsertAsync(connection, transaction, "DonHang", Rows(store.DonHangs,
            ("MaDonHang", typeof(string), x => x.MaDonHang),
            ("MaKhachHang", typeof(string), x => x.MaKhachHang),
            ("MaNV", typeof(string), x => x.MaNV),
            ("NgayDat", typeof(DateTime), x => x.NgayDat),
            ("TrangThai", typeof(string), x => x.TrangThai),
            ("TongTien", typeof(decimal), x => x.TongTien)));
        await InsertAsync(connection, transaction, "ChiTietDonHang", Rows(store.ChiTietDonHangs,
            ("MaDonHang", typeof(string), x => x.MaDonHang),
            ("MaSP", typeof(string), x => x.MaSP),
            ("SoLuong", typeof(int), x => x.SoLuong),
            ("DonGia", typeof(decimal), x => x.DonGia),
            ("ThanhTien", typeof(decimal), x => x.ThanhTien)));
        await InsertAsync(connection, transaction, "PhieuNhap", Rows(store.PhieuNhaps,
            ("MaPhieuNhap", typeof(string), x => x.MaPhieuNhap),
            ("MaNCC", typeof(string), x => x.MaNCC),
            ("MaKho", typeof(string), x => x.MaKho),
            ("MaNV", typeof(string), x => x.MaNV),
            ("NgayNhap", typeof(DateTime), x => x.NgayNhap),
            ("TongTien", typeof(decimal), x => x.TongTien)));
        await InsertAsync(connection, transaction, "ChiTietPhieuNhap", Rows(store.ChiTietPhieuNhaps,
            ("MaPhieuNhap", typeof(string), x => x.MaPhieuNhap),
            ("MaSP", typeof(string), x => x.MaSP),
            ("SoLuong", typeof(int), x => x.SoLuong),
            ("DonGiaNhap", typeof(decimal), x => x.DonGiaNhap),
            ("ThanhTien", typeof(decimal), x => x.ThanhTien)));
        await InsertAsync(connection, transaction, "TonKho", Rows(store.TonKhos,
            ("MaKho", typeof(string), x => x.MaKho),
            ("MaSP", typeof(string), x => x.MaSP),
            ("SoLuongTon", typeof(int), x => x.SoLuongTon),
            ("SoLuongDaBan", typeof(int), x => x.SoLuongDaBan),
            ("MucToiThieu", typeof(int), x => x.MucToiThieu),
            ("MucToiDa", typeof(int), x => x.MucToiDa)));
        await InsertAsync(connection, transaction, "CongNo", Rows(store.CongNos,
            ("MaCongNo", typeof(string), x => x.MaCongNo),
            ("MaNV", typeof(string), x => x.MaNV),
            ("MaKhachHang", typeof(string), x => x.MaKhachHang),
            ("MaNCC", typeof(string), x => x.MaNCC),
            ("LoaiCongNo", typeof(string), x => x.LoaiCongNo),
            ("NgayPhatSinh", typeof(DateTime), x => x.NgayPhatSinh),
            ("SoTienNo", typeof(decimal), x => x.SoTienNo),
            ("HanThanhToan", typeof(DateTime), x => x.HanThanhToan),
            ("TrangThai", typeof(string), x => x.TrangThai)));

        await transaction.CommitAsync();
    }

    private static DataTable Rows<T>(IEnumerable<T> rows, params (string Name, Type Type, Func<T, object?> Value)[] fields)
    {
        var table = new DataTable();
        foreach (var field in fields)
        {
            table.Columns.Add(field.Name, field.Type);
        }

        foreach (var item in rows)
        {
            table.Rows.Add(fields.Select(field => field.Value(item) ?? DBNull.Value).ToArray());
        }

        return table;
    }

    private static async Task InsertAsync(SqlConnection connection, SqlTransaction transaction, string tableName, DataTable data)
    {
        if (data.Rows.Count == 0)
        {
            return;
        }

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, transaction)
        {
            DestinationTableName = $"dbo.{tableName}"
        };
        foreach (DataColumn column in data.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(data);
    }

    private static async Task LoadAsync(SqlConnection connection, DataStore store)
    {
        var categories = await ReadAsync(connection, "SELECT MaLoai, TenLoai, MoTa FROM dbo.LoaiKhachHang", r => new LoaiKhachHang
        {
            MaLoai = Text(r, "MaLoai") ?? string.Empty,
            TenLoai = Text(r, "TenLoai") ?? string.Empty,
            MoTa = Text(r, "MoTa")
        });
        var employees = await ReadAsync(connection,
            "SELECT MaNV, HoTen, SoDienThoai, Email, TenDangNhap, VaiTro FROM dbo.NhanVien", r => new NhanVien
            {
                MaNV = Text(r, "MaNV") ?? string.Empty,
                HoTen = Text(r, "HoTen") ?? string.Empty,
                SoDienThoai = Text(r, "SoDienThoai"),
                Email = Text(r, "Email"),
                TenDangNhap = Text(r, "TenDangNhap"),
                VaiTro = Text(r, "VaiTro")
            });
        var customers = await ReadAsync(connection,
            """
            SELECT kh.MaKhachHang, kh.MaLoai, kh.MaNV, kh.HoTen, kh.SoDienThoai, kh.Email, kh.DiaChi,
                   lk.TenLoai, COALESCE(tt.DiemTichLuy, 0) AS DiemTichLuy
            FROM dbo.KhachHang kh
            LEFT JOIN dbo.LoaiKhachHang lk ON lk.MaLoai = kh.MaLoai
            LEFT JOIN dbo.TheThanhVien tt ON tt.MaKhachHang = kh.MaKhachHang
            """, r => new KhachHang
            {
                MaKhachHang = Text(r, "MaKhachHang") ?? string.Empty,
                MaLoai = Text(r, "MaLoai"),
                MaNV = Text(r, "MaNV"),
                HoTen = Text(r, "HoTen") ?? string.Empty,
                SoDienThoai = Text(r, "SoDienThoai"),
                Email = Text(r, "Email"),
                DiaChi = Text(r, "DiaChi"),
                TenLoai = Text(r, "TenLoai"),
                DiemTichLuy = Number(r, "DiemTichLuy")
            });
        var memberCards = await ReadAsync(connection,
            "SELECT MaThe, MaKhachHang, NgayLap, DiemTichLuy FROM dbo.TheThanhVien", r => new TheThanhVien
            {
                MaThe = Text(r, "MaThe") ?? string.Empty,
                MaKhachHang = Text(r, "MaKhachHang"),
                NgayLap = Date(r, "NgayLap"),
                DiemTichLuy = Number(r, "DiemTichLuy")
            });
        var categoriesOfProducts = await ReadAsync(connection,
            "SELECT MaDanhMuc, TenDanhMuc, MoTa FROM dbo.DanhMuc", r => new DanhMuc
            {
                MaDanhMuc = Text(r, "MaDanhMuc") ?? string.Empty,
                TenDanhMuc = Text(r, "TenDanhMuc") ?? string.Empty,
                MoTa = Text(r, "MoTa")
            });
        var suppliers = await ReadAsync(connection,
            "SELECT MaNCC, TenNCC, DiaChi, SoDienThoai, Email FROM dbo.NhaCungCap", r => new NhaCungCap
            {
                MaNCC = Text(r, "MaNCC") ?? string.Empty,
                TenNCC = Text(r, "TenNCC") ?? string.Empty,
                DiaChi = Text(r, "DiaChi"),
                SoDienThoai = Text(r, "SoDienThoai"),
                Email = Text(r, "Email")
            });
        var warehouses = await ReadAsync(connection, "SELECT MaKho, TenKho, DiaChi FROM dbo.Kho", r => new Kho
        {
            MaKho = Text(r, "MaKho") ?? string.Empty,
            TenKho = Text(r, "TenKho") ?? string.Empty,
            DiaChi = Text(r, "DiaChi")
        });
        var inventories = await ReadAsync(connection,
            """
            SELECT tk.MaKho, tk.MaSP, tk.SoLuongTon, tk.SoLuongDaBan, tk.MucToiThieu, tk.MucToiDa,
                   k.TenKho, sp.TenSP, sp.DonViTinh,
                   CASE WHEN tk.SoLuongTon < tk.MucToiThieu THEN N'Dưới tối thiểu'
                        WHEN tk.SoLuongTon > tk.MucToiDa THEN N'Vượt tối đa'
                        ELSE N'Bình thường' END AS TrangThaiCanhBao
            FROM dbo.TonKho tk
            LEFT JOIN dbo.Kho k ON k.MaKho = tk.MaKho
            LEFT JOIN dbo.SanPham sp ON sp.MaSP = tk.MaSP
            """, r => new TonKho
            {
                MaKho = Text(r, "MaKho") ?? string.Empty,
                MaSP = Text(r, "MaSP") ?? string.Empty,
                SoLuongTon = Number(r, "SoLuongTon"),
                SoLuongDaBan = Number(r, "SoLuongDaBan"),
                MucToiThieu = Number(r, "MucToiThieu"),
                MucToiDa = Number(r, "MucToiDa"),
                TenKho = Text(r, "TenKho"),
                TenSP = Text(r, "TenSP"),
                DonViTinh = Text(r, "DonViTinh"),
                TrangThaiCanhBao = Text(r, "TrangThaiCanhBao")
            });
        var products = await ReadAsync(connection,
            """
            SELECT sp.MaSP, sp.MaDanhMuc, sp.TenSP, sp.MoTa, sp.GiaBan, sp.HanSuDung, sp.DonViTinh,
                   dm.TenDanhMuc, COALESCE(kho.SoLuongTon, 0) AS SoLuongTon
            FROM dbo.SanPham sp
            LEFT JOIN dbo.DanhMuc dm ON dm.MaDanhMuc = sp.MaDanhMuc
            LEFT JOIN (SELECT MaSP, SUM(SoLuongTon) AS SoLuongTon FROM dbo.TonKho GROUP BY MaSP) kho
                ON kho.MaSP = sp.MaSP
            """, r => new SanPham
            {
                MaSP = Text(r, "MaSP") ?? string.Empty,
                MaDanhMuc = Text(r, "MaDanhMuc"),
                TenSP = Text(r, "TenSP") ?? string.Empty,
                MoTa = Text(r, "MoTa"),
                GiaBan = Amount(r, "GiaBan"),
                HanSuDung = Date(r, "HanSuDung"),
                DonViTinh = Text(r, "DonViTinh"),
                TenDanhMuc = Text(r, "TenDanhMuc"),
                SoLuongTon = Number(r, "SoLuongTon")
            });
        var orderDetails = await ReadAsync(connection,
            """
            SELECT ct.MaDonHang, ct.MaSP, ct.SoLuong, ct.DonGia, ct.ThanhTien, sp.TenSP, sp.DonViTinh
            FROM dbo.ChiTietDonHang ct
            LEFT JOIN dbo.SanPham sp ON sp.MaSP = ct.MaSP
            """, r => new ChiTietDonHang
            {
                MaDonHang = Text(r, "MaDonHang") ?? string.Empty,
                MaSP = Text(r, "MaSP") ?? string.Empty,
                SoLuong = Number(r, "SoLuong"),
                DonGia = Amount(r, "DonGia"),
                ThanhTien = Amount(r, "ThanhTien"),
                TenSP = Text(r, "TenSP"),
                DonViTinh = Text(r, "DonViTinh")
            });
        var orders = await ReadAsync(connection,
            """
            SELECT dh.MaDonHang, dh.MaKhachHang, dh.MaNV, dh.NgayDat, dh.TrangThai, dh.TongTien,
                   COALESCE(dh.TenNguoiNhan, kh.HoTen) AS TenKhachHang,
                   COALESCE(dh.TenNguoiNhan, kh.HoTen) AS TenNguoiNhan,
                   COALESCE(dh.SoDienThoaiGiaoHang, kh.SoDienThoai) AS SoDienThoai,
                   dh.SoDienThoaiGiaoHang,
                   dh.DiaChiGiaoHang, dh.PhuongThucThanhToan, dh.MaVoucher, dh.TienGiamGia
            FROM dbo.DonHang dh
            LEFT JOIN dbo.KhachHang kh ON kh.MaKhachHang = dh.MaKhachHang
            """, r => new DonHang
            {
                MaDonHang = Text(r, "MaDonHang") ?? string.Empty,
                MaKhachHang = Text(r, "MaKhachHang"),
                MaNV = Text(r, "MaNV"),
                NgayDat = Date(r, "NgayDat"),
                TrangThai = Text(r, "TrangThai"),
                TongTien = Amount(r, "TongTien"),
                TenKhachHang = Text(r, "TenKhachHang"),
                SoDienThoai = Text(r, "SoDienThoai"),
                TenNguoiNhan = Text(r, "TenNguoiNhan"),
                SoDienThoaiGiaoHang = Text(r, "SoDienThoaiGiaoHang"),
                DiaChiGiaoHang = Text(r, "DiaChiGiaoHang"),
                PhuongThucThanhToan = Text(r, "PhuongThucThanhToan"),
                MaVoucher = Text(r, "MaVoucher"),
                TienGiamGia = Amount(r, "TienGiamGia")
            });
        var importDetails = await ReadAsync(connection,
            """
            SELECT ct.MaPhieuNhap, ct.MaSP, ct.SoLuong, ct.DonGiaNhap, ct.ThanhTien, sp.TenSP
            FROM dbo.ChiTietPhieuNhap ct
            LEFT JOIN dbo.SanPham sp ON sp.MaSP = ct.MaSP
            """, r => new ChiTietPhieuNhap
            {
                MaPhieuNhap = Text(r, "MaPhieuNhap") ?? string.Empty,
                MaSP = Text(r, "MaSP") ?? string.Empty,
                SoLuong = Number(r, "SoLuong"),
                DonGiaNhap = Amount(r, "DonGiaNhap"),
                ThanhTien = Amount(r, "ThanhTien"),
                TenSP = Text(r, "TenSP")
            });
        var imports = await ReadAsync(connection,
            """
            SELECT pn.MaPhieuNhap, pn.MaNCC, pn.MaKho, pn.MaNV, pn.NgayNhap, pn.TongTien,
                   ncc.TenNCC, k.TenKho
            FROM dbo.PhieuNhap pn
            LEFT JOIN dbo.NhaCungCap ncc ON ncc.MaNCC = pn.MaNCC
            LEFT JOIN dbo.Kho k ON k.MaKho = pn.MaKho
            """, r => new PhieuNhap
            {
                MaPhieuNhap = Text(r, "MaPhieuNhap") ?? string.Empty,
                MaNCC = Text(r, "MaNCC"),
                MaKho = Text(r, "MaKho"),
                MaNV = Text(r, "MaNV"),
                NgayNhap = Date(r, "NgayNhap"),
                TongTien = Amount(r, "TongTien"),
                TenNCC = Text(r, "TenNCC"),
                TenKho = Text(r, "TenKho")
            });
        var debts = await ReadAsync(connection,
            """
            SELECT cn.MaCongNo, cn.MaNV, cn.MaKhachHang, cn.MaNCC, cn.MaDonHang, cn.LoaiCongNo,
                   cn.NgayPhatSinh, cn.SoTienNo, cn.HanThanhToan, cn.TrangThai,
                   COALESCE(kh.HoTen, ncc.TenNCC) AS DoiTuongTen,
                   CASE WHEN cn.SoTienNo > 0 AND cn.TrangThai <> N'Đã thanh toán'
                                  AND (cn.TrangThai = N'Quá hạn' OR cn.HanThanhToan < CONVERT(date, GETDATE()))
                        THEN N'Quá hạn thanh toán'
                        ELSE N'Bình thường' END AS CanhBao
            FROM dbo.CongNo cn
            LEFT JOIN dbo.KhachHang kh ON kh.MaKhachHang = cn.MaKhachHang
            LEFT JOIN dbo.NhaCungCap ncc ON ncc.MaNCC = cn.MaNCC
            """, r => new CongNo
            {
                MaCongNo = Text(r, "MaCongNo") ?? string.Empty,
                MaNV = Text(r, "MaNV"),
                MaKhachHang = Text(r, "MaKhachHang"),
                MaNCC = Text(r, "MaNCC"),
                MaDonHang = Text(r, "MaDonHang"),
                LoaiCongNo = Text(r, "LoaiCongNo"),
                NgayPhatSinh = Date(r, "NgayPhatSinh"),
                SoTienNo = Amount(r, "SoTienNo"),
                HanThanhToan = Date(r, "HanThanhToan"),
                TrangThai = Text(r, "TrangThai"),
                DoiTuongTen = Text(r, "DoiTuongTen"),
                CanhBao = Text(r, "CanhBao")
            });
        var vouchers = await ReadAsync(connection,
            """
            SELECT MaVoucher, MaKhachHang, GiaTri, NgayTao, NgayHetHan, MaDonHangSuDung
            FROM dbo.Voucher
            """, r => new Voucher
            {
                MaVoucher = Text(r, "MaVoucher") ?? string.Empty,
                MaKhachHang = Text(r, "MaKhachHang") ?? string.Empty,
                GiaTri = Amount(r, "GiaTri"),
                NgayTao = Date(r, "NgayTao") ?? DateTime.MinValue,
                NgayHetHan = Date(r, "NgayHetHan") ?? DateTime.MinValue,
                MaDonHangSuDung = Text(r, "MaDonHangSuDung")
            });

        foreach (var order in orders)
        {
            order.ChiTiet = orderDetails.Where(d => d.MaDonHang == order.MaDonHang).ToList();
        }

        foreach (var import in imports)
        {
            import.ChiTiet = importDetails.Where(d => d.MaPhieuNhap == import.MaPhieuNhap).ToList();
        }

        Replace(store.LoaiKhachHangs, categories);
        Replace(store.NhanViens, employees);
        Replace(store.KhachHangs, customers);
        Replace(store.TheThanhViens, memberCards);
        Replace(store.DanhMucs, categoriesOfProducts);
        Replace(store.NhaCungCaps, suppliers);
        Replace(store.Khos, warehouses);
        Replace(store.SanPhams, products);
        Replace(store.DonHangs, orders);
        Replace(store.ChiTietDonHangs, orderDetails);
        Replace(store.PhieuNhaps, imports);
        Replace(store.ChiTietPhieuNhaps, importDetails);
        Replace(store.TonKhos, inventories);
        Replace(store.CongNos, debts);
        Replace(store.Vouchers, vouchers);
    }

    private static async Task<List<T>> ReadAsync<T>(SqlConnection connection, string sql, Func<SqlDataReader, T> map)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<T>();
        while (await reader.ReadAsync())
        {
            results.Add(map(reader));
        }

        return results;
    }

    private static async Task<List<T>> ReadAsync<T>(
        SqlConnection connection,
        string sql,
        Action<SqlCommand> configure,
        Func<SqlDataReader, T> map)
    {
        await using var command = new SqlCommand(sql, connection);
        configure(command);
        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<T>();
        while (await reader.ReadAsync())
        {
            results.Add(map(reader));
        }

        return results;
    }

    private static void Replace<T>(List<T> destination, IEnumerable<T> items)
    {
        destination.Clear();
        destination.AddRange(items);
    }

    private static string? Text(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static int Number(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal Amount(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? Date(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}
