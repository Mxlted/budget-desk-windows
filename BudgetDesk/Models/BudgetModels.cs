namespace BudgetDesk.Models;

public enum TransactionType { Expense, Income }
public enum TransactionSource { Manual, Import, Recurring }

public class Transaction
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public string Merchant { get; set; } = "";
    public string Category { get; set; } = "";
    public double Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Account { get; set; } = "";
    public string? Notes { get; set; }
    public TransactionSource Source { get; set; }
}

public class RecurringPurchase
{
    public string Id { get; set; } = "";
    public string Merchant { get; set; } = "";
    public string Category { get; set; } = "";
    public double Amount { get; set; }
    public TransactionType Type { get; set; }
    public int Day { get; set; } = 1;
    public string Account { get; set; } = "";
    public bool Active { get; set; } = true;
    public string StartMonth { get; set; } = "";
    public string? EndMonth { get; set; }
    public string? Notes { get; set; }
}

public class BudgetCategory
{
    public string CategoryName { get; set; } = "";
    public double MonthlyLimit { get; set; }
    public string Color { get; set; } = "#9ca3af";
    public List<string> Keywords { get; set; } = [];
}

public class SavingsGoal
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public double Target { get; set; }
    public double Saved { get; set; }
    public double MonthlyContribution { get; set; }
    public string Color { get; set; } = "#3b82f6";
}

public class BudgetState
{
    public List<Transaction> Transactions { get; set; } = [];
    public List<RecurringPurchase> Recurring { get; set; } = [];
    public List<BudgetCategory> Budgets { get; set; } = [];
    public bool CategoryLimitsEnabled { get; set; } = true;
    public List<SavingsGoal> SavingsGoals { get; set; } = [];
    public List<string> Accounts { get; set; } = [];
    public double PlannedMonthlyIncome { get; set; }
}

public class BudgetProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public BudgetState State { get; set; } = new();
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public class MonthSummary
{
    public List<Transaction> Rows { get; set; } = [];
    public double ActualIncome { get; set; }
    public double PlannedIncome { get; set; }
    public double Income { get; set; }
    public bool UsesPlannedIncome { get; set; }
    public double Expenses { get; set; }
    public double Recurring { get; set; }
    public double RecurringIncome { get; set; }
    public double ManualExpenses { get; set; }
    public double Remaining { get; set; }
    public double SavingsRate { get; set; }
}

public class CategoryBreakdownItem
{
    public string Category { get; set; } = "";
    public double Value { get; set; }
    public string Color { get; set; } = "#9ca3af";
}

public class BudgetBarItem
{
    public string Category { get; set; } = "";
    public double Spent { get; set; }
    public double Budget { get; set; }
    public string Color { get; set; } = "#9ca3af";
    public double Percent => Budget > 0 ? Spent / Budget * 100 : 0;
}

public class TrendPoint
{
    public string Month { get; set; } = "";
    public double Income { get; set; }
    public double Expenses { get; set; }
    public double RecurringAmount { get; set; }
}

public class YearlySummary
{
    public string Year { get; set; } = "";
    public string PeriodLabel { get; set; } = "";
    public int MonthCount { get; set; }
    public double Income { get; set; }
    public double Expenses { get; set; }
    public double Recurring { get; set; }
    public double Remaining { get; set; }
    public double SavingsRate { get; set; }
    public double AverageMonthlyExpenses { get; set; }
    public int ActiveMonthCount { get; set; }
    public string BestMonthLabel { get; set; } = "";
    public double BestMonthRemaining { get; set; }
    public string HighestExpenseMonthLabel { get; set; } = "";
    public double HighestExpenseMonthExpenses { get; set; }
    public List<CategoryBreakdownItem> YearlyBreakdown { get; set; } = [];
}

public class StatementRow
{
    public string Date { get; set; } = "";
    public string Merchant { get; set; } = "";
    public string Category { get; set; } = "";
    public double Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Account { get; set; } = "";
    public string? Notes { get; set; }
}
