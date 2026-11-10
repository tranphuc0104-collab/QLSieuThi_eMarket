using System.Data;
using System.Security.Cryptography;
using System.Text;
using EMarket.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace EMarket.Services;

public class AccountService
{
    private readonly string _connectionString;
    private readonly IPasswordHasher<UserAccount> _passwordHasher;
    private readonly DataStore _store;

    public AccountService(IConfiguration configuration, IPasswordHasher<UserAccount> passwordHasher, DataStore store)
    {
        _connectionString = configuration.GetConnectionString("EMarket")
            ?? throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:EMarket.");
        _passwordHasher = passwordHasher;
        _store = store;
    }

    public async Task<UserAccount?> SignInAsync(string userName, string password)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var command = new SqlCommand(
            """
            SELECT a.TenDangNhap, a.MatKhauHash, a.VaiTro,
                   COALESCE(kh.HoTen, nv.HoTen, a.TenDangNhap) AS HoTen,
                   a.MaKhachHang
            FROM dbo.TaiKhoan a
            LEFT JOIN dbo.KhachHang kh ON kh.MaKhachHang = a.MaKhachHang
            LEFT JOIN dbo.NhanVien nv ON nv.MaNV = a.MaNV
            WHERE a.TenDangNhap = @UserName;
            """, connection))
        {
            command.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = userName.Trim();
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var account = new UserAccount
                {
                    UserName = reader.GetString(0),
                    PasswordHash = reader.GetString(1),
                    Role = reader.GetString(2),
                    DisplayName = reader.GetString(3),
                    CustomerId = reader.IsDBNull(4) ? null : reader.GetString(4)
                };
                var verification = _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
                return verification == PasswordVerificationResult.Failed ? null : account;
            }
        }

        return await SignInLegacyEmployeeAsync(connection, userName.Trim(), password);
    }

    public async Task<bool> RegisterCustomerAsync(RegisterViewModel model)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        await using (var duplicateCheck = new SqlCommand(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.TaiKhoan WITH (UPDLOCK, HOLDLOCK) WHERE TenDangNhap = @UserName
                UNION ALL
                SELECT 1 FROM dbo.NhanVien WITH (UPDLOCK, HOLDLOCK) WHERE TenDangNhap = @UserName
            ) THEN 1 ELSE 0 END;
            """, connection, transaction))
        {
            duplicateCheck.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = model.UserName.Trim();
            if ((int)(await duplicateCheck.ExecuteScalarAsync())! == 1)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        var customerId = "KH" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        var cardId = "THE" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        var account = new UserAccount
        {
            UserName = model.UserName.Trim(),
            Role = "Customer",
            DisplayName = model.FullName.Trim(),
            CustomerId = customerId
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, model.Password);

        await using (var insertCustomer = new SqlCommand(
            """
            INSERT INTO dbo.KhachHang (MaKhachHang, MaLoai, HoTen, SoDienThoai, Email, DiaChi)
            VALUES (@CustomerId, (SELECT TOP (1) MaLoai FROM dbo.LoaiKhachHang ORDER BY MaLoai),
                    @FullName, @Phone, @Email, @Address);
            INSERT INTO dbo.TheThanhVien (MaThe, MaKhachHang, NgayLap, DiemTichLuy)
            VALUES (@CardId, @CustomerId, CONVERT(date, GETDATE()), 0);
            INSERT INTO dbo.TaiKhoan (TenDangNhap, MatKhauHash, VaiTro, MaKhachHang)
            VALUES (@UserName, @PasswordHash, 'Customer', @CustomerId);
            """, connection, transaction))
        {
            insertCustomer.Parameters.Add("@CustomerId", SqlDbType.VarChar, 50).Value = customerId;
            insertCustomer.Parameters.Add("@CardId", SqlDbType.VarChar, 50).Value = cardId;
            insertCustomer.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = model.FullName.Trim();
            insertCustomer.Parameters.Add("@Phone", SqlDbType.VarChar, 20).Value = (object?)model.PhoneNumber?.Trim() ?? DBNull.Value;
            insertCustomer.Parameters.Add("@Email", SqlDbType.VarChar, 100).Value = (object?)model.Email?.Trim() ?? DBNull.Value;
            insertCustomer.Parameters.Add("@Address", SqlDbType.NVarChar, 255).Value = (object?)model.Address?.Trim() ?? DBNull.Value;
            insertCustomer.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = account.UserName;
            insertCustomer.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = account.PasswordHash;
            await insertCustomer.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        var customerType = _store.LoaiKhachHangs.OrderBy(x => x.MaLoai).FirstOrDefault();
        _store.KhachHangs.Add(new KhachHang
        {
            MaKhachHang = customerId,
            MaLoai = customerType?.MaLoai,
            HoTen = model.FullName.Trim(),
            SoDienThoai = model.PhoneNumber?.Trim(),
            Email = model.Email?.Trim(),
            DiaChi = model.Address?.Trim(),
            TenLoai = customerType?.TenLoai,
            DiemTichLuy = 0
        });
        _store.TheThanhViens.Add(new TheThanhVien
        {
            MaThe = cardId,
            MaKhachHang = customerId,
            NgayLap = DateTime.Today,
            DiemTichLuy = 0
        });
        return true;
    }

    private async Task<UserAccount?> SignInLegacyEmployeeAsync(SqlConnection connection, string userName, string password)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        string? employeeId = null;
        string? fullName = null;
        string? storedPassword = null;

        await using (var command = new SqlCommand(
            """
            SELECT MaNV, HoTen, MatKhau
            FROM dbo.NhanVien WITH (UPDLOCK, HOLDLOCK)
            WHERE TenDangNhap = @UserName;
            """, connection, transaction))
        {
            command.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = userName;
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                employeeId = reader.GetString(0);
                fullName = reader.GetString(1);
                storedPassword = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        if (employeeId is null || storedPassword is null || !FixedTimeEquals(storedPassword, password))
        {
            await transaction.RollbackAsync();
            return null;
        }

        var account = new UserAccount
        {
            UserName = userName,
            Role = "Admin",
            DisplayName = fullName!,
            PasswordHash = string.Empty
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, password);

        await using (var command = new SqlCommand(
            """
            INSERT INTO dbo.TaiKhoan (TenDangNhap, MatKhauHash, VaiTro, MaNV)
            VALUES (@UserName, @PasswordHash, 'Admin', @EmployeeId);
            UPDATE dbo.NhanVien SET MatKhau = NULL WHERE MaNV = @EmployeeId;
            """, connection, transaction))
        {
            command.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = userName;
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = account.PasswordHash;
            command.Parameters.Add("@EmployeeId", SqlDbType.VarChar, 50).Value = employeeId;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return account;
    }

    private static bool FixedTimeEquals(string storedValue, string suppliedValue)
    {
        var storedBytes = Encoding.UTF8.GetBytes(storedValue);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedValue);
        return storedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(storedBytes, suppliedBytes);
    }
}
