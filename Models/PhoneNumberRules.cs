using System.Text.RegularExpressions;

namespace EMarket.Models;

public static class PhoneNumberRules
{
    public const string VietnamesePhonePattern = @"^(?:0\d{9,10}|\+84\d{9,10})$";
    public const string InvalidVietnamesePhoneMessage =
        "Số điện thoại không hợp lệ. Nhập số Việt Nam gồm 10 hoặc 11 chữ số, bắt đầu bằng 0 hoặc +84.";

    public static bool IsValidVietnamesePhoneNumber(string? value)
    {
        return string.IsNullOrEmpty(value) || Regex.IsMatch(value, VietnamesePhonePattern);
    }
}
