using System.Text.RegularExpressions;

namespace EMarket.Models;

public static class EmailAddressRules
{
    public const string Pattern = @"^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)+$";
    public const string InvalidMessage = "Email không đúng định dạng. Ví dụ: ten@gmail.com.";

    public static bool IsValid(string? value)
    {
        return string.IsNullOrEmpty(value) || Regex.IsMatch(value, Pattern);
    }
}
