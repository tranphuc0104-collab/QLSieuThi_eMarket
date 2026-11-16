namespace EMarket.Models;

public class CustomerDebtLookupViewModel
{
    public string? Keyword { get; set; }
    public List<CustomerDebtSummaryViewModel> Customers { get; set; } = new();
}

public class CustomerDebtSummaryViewModel
{
    public KhachHang Customer { get; set; } = new();
    public List<CongNo> Debts { get; set; } = new();
    public decimal TotalDebt { get; set; }
    public decimal OverdueDebt { get; set; }
}
