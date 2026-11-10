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

        if (!await HasBusinessRowsAsync(connection))
        {
            await SeedDemoDataAsync(connection, store);
        }

        await MigrateLegacyEmployeePasswordsAsync(connection);
        await LoadAsync(connection, store);
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
                   kh.HoTen AS TenKhachHang, kh.SoDienThoai
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
                SoDienThoai = Text(r, "SoDienThoai")
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
            SELECT cn.MaCongNo, cn.MaNV, cn.MaKhachHang, cn.MaNCC, cn.LoaiCongNo,
                   cn.NgayPhatSinh, cn.SoTienNo, cn.HanThanhToan, cn.TrangThai,
                   COALESCE(kh.HoTen, ncc.TenNCC) AS DoiTuongTen,
                   CASE WHEN cn.TrangThai = N'Quá hạn' OR cn.HanThanhToan < CONVERT(date, GETDATE())
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
                LoaiCongNo = Text(r, "LoaiCongNo"),
                NgayPhatSinh = Date(r, "NgayPhatSinh"),
                SoTienNo = Amount(r, "SoTienNo"),
                HanThanhToan = Date(r, "HanThanhToan"),
                TrangThai = Text(r, "TrangThai"),
                DoiTuongTen = Text(r, "DoiTuongTen"),
                CanhBao = Text(r, "CanhBao")
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
