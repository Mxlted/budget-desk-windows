using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using BudgetDesk.Models;
using BudgetDesk.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace BudgetDesk.ViewModels;

public partial class MainViewModel : ObservableObject
{
    const string AllCategoriesFilter = "All categories";

    readonly StorageService _storage = new();
    readonly BudgetState _fallbackState = SampleData.CreateInitialBudgetState();
    bool _initialized;

    [ObservableProperty] string _selectedTab = "Dashboard";
    [ObservableProperty] string _selectedMonth = BudgetCalculator.CurrentMonthKey();
    [ObservableProperty] string _selectedMonthDisplay = "";
    [ObservableProperty] string _statusMessage = "";

    // Profiles
    [ObservableProperty] ObservableCollection<BudgetProfile> _profiles = [];
    [ObservableProperty] BudgetProfile? _activeProfile;
    [ObservableProperty] string _newProfileName = "";
    [ObservableProperty] string _profileTemplate = "empty";
    [ObservableProperty] string _renameProfileName = "";

    // Month Summary
    [ObservableProperty] string _incomeDisplay = "$0";
    [ObservableProperty] string _incomeDetail = "No income";
    [ObservableProperty] string _expensesDisplay = "$0";
    [ObservableProperty] string _recurringDisplay = "$0 recurring";
    [ObservableProperty] string _cashFlowDisplay = "$0";
    [ObservableProperty] string _cashFlowDetail = "0% left";
    [ObservableProperty] bool _cashFlowPositive = true;
    [ObservableProperty] string _transactionCountDisplay = "0";
    [ObservableProperty] string _transactionCountDetail = "";

    // Dashboard data
    [ObservableProperty] ISeries[] _trendSeries = [];
    [ObservableProperty] Axis[] _trendXAxes = [];
    [ObservableProperty] ISeries[] _categoryPieSeries = [];
    [ObservableProperty] ObservableCollection<BudgetBarItem> _budgetBars = [];
    [ObservableProperty] ObservableCollection<SavingsGoal> _savingsGoals = [];
    [ObservableProperty] bool _categoryLimitsEnabled = true;
    [ObservableProperty] bool _hasCategoryLimitValues = true;

    // Yearly summary
    [ObservableProperty] string _yearlyPeriodLabel = "";
    [ObservableProperty] string _yearlyIncome = "$0";
    [ObservableProperty] string _yearlyExpenses = "$0";
    [ObservableProperty] string _yearlyCashFlow = "$0";
    [ObservableProperty] double _yearlyCashFlowValue;
    [ObservableProperty] string _yearlySaved = "0%";
    [ObservableProperty] double _yearlySavingsRateValue;
    [ObservableProperty] string _yearlyRecurring = "$0";
    [ObservableProperty] string _yearlyAvgMonthly = "$0";
    [ObservableProperty] string _yearlyBestMonth = "$0";
    [ObservableProperty] string _yearlyBestMonthLabel = "";
    [ObservableProperty] string _yearlyHighestSpend = "$0";
    [ObservableProperty] string _yearlyHighestSpendLabel = "";
    [ObservableProperty] string _yearlyActiveMonths = "0 active / 0 months";
    [ObservableProperty] ISeries[] _yearlyPieSeries = [];

    public SolidColorPaint ChartLegendTextPaint { get; } = new(SKColor.Parse("#f5f5f5"));
    public SolidColorPaint ChartTooltipTextPaint { get; } = new(SKColor.Parse("#f5f5f5"));
    public SolidColorPaint ChartTooltipBackgroundPaint { get; } = new(SKColor.Parse("#1e1e1e"));

    // Transactions
    [ObservableProperty] ObservableCollection<Transaction> _filteredTransactions = [];
    [ObservableProperty] string _searchQuery = "";
    [ObservableProperty] string? _categoryFilter = AllCategoriesFilter;
    [ObservableProperty] ObservableCollection<string> _categoryOptions = [];
    [ObservableProperty] ObservableCollection<string> _expenseCategoryOptions = [];
    [ObservableProperty] ObservableCollection<string> _accountOptions = [];

    // Add purchase form
    [ObservableProperty] string _purchaseDate = "";
    [ObservableProperty] string _purchaseMerchant = "";
    [ObservableProperty] double _purchaseAmount;
    [ObservableProperty] string _purchaseCategory = "Groceries";
    [ObservableProperty] TransactionType _purchaseType = TransactionType.Expense;
    [ObservableProperty] string _purchaseAccount = "Credit Card";
    [ObservableProperty] string _purchaseNotes = "";

    // Edit purchase
    [ObservableProperty] Transaction? _editingTransaction;
    [ObservableProperty] string _editDate = "";
    [ObservableProperty] string _editMerchant = "";
    [ObservableProperty] double _editAmount;
    [ObservableProperty] string _editCategory = "Groceries";
    [ObservableProperty] TransactionType _editType = TransactionType.Expense;
    [ObservableProperty] string _editAccount = "Credit Card";
    [ObservableProperty] string _editNotes = "";

    // Recurring
    [ObservableProperty] ObservableCollection<RecurringPurchase> _recurringItems = [];
    [ObservableProperty] string _recurringMerchant = "";
    [ObservableProperty] double _recurringAmount;
    [ObservableProperty] int _recurringDay = 1;
    [ObservableProperty] string _recurringCategory = "Utilities";
    [ObservableProperty] TransactionType _recurringType = TransactionType.Expense;
    [ObservableProperty] string _recurringAccount = "Checking";
    [ObservableProperty] string _recurringStartMonth = "";
    [ObservableProperty] string _recurringEndMonth = "";
    [ObservableProperty] string _recurringNotes = "";

    // Edit recurring
    [ObservableProperty] RecurringPurchase? _editingRecurring;
    [ObservableProperty] string _editRecMerchant = "";
    [ObservableProperty] double _editRecAmount;
    [ObservableProperty] int _editRecDay = 1;
    [ObservableProperty] string _editRecCategory = "Utilities";
    [ObservableProperty] TransactionType _editRecType = TransactionType.Expense;
    [ObservableProperty] string _editRecAccount = "Checking";
    [ObservableProperty] string _editRecStartMonth = "";
    [ObservableProperty] string _editRecEndMonth = "";
    [ObservableProperty] string _editRecNotes = "";

    // Categories
    [ObservableProperty] ObservableCollection<BudgetCategory> _categoryLimitBudgets = [];
    [ObservableProperty] string _newCategoryName = "";
    [ObservableProperty] double _newCategoryLimit;

    // Import
    [ObservableProperty] ObservableCollection<StatementRow> _importPreview = [];
    [ObservableProperty] string _statementType = "bank";
    [ObservableProperty] string _importAccount = "Checking";

    BudgetState State => ActiveProfile?.State ?? _fallbackState;

    public MainViewModel()
    {
        var profiles = _storage.LoadProfiles();
        Profiles = new ObservableCollection<BudgetProfile>(profiles);
        ActiveProfile = Profiles.FirstOrDefault();
        RenameProfileName = ActiveProfile?.Name ?? "";
        SelectedMonth = BudgetCalculator.CurrentMonthKey();
        RecurringStartMonth = SelectedMonth;
        PurchaseDate = $"{SelectedMonth}-15";

        PropertyChanged += OnPropertyChanged;
        _initialized = true;
        RefreshAll();
    }

    partial void OnActiveProfileChanged(BudgetProfile? value)
    {
        if (!_initialized) return;

        RenameProfileName = value?.Name ?? "";
        ImportPreview.Clear();
        SearchQuery = "";
        CategoryFilter = AllCategoriesFilter;
        EditingTransaction = null;
        EditingRecurring = null;
        RefreshAll();
    }

    void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchQuery) || e.PropertyName == nameof(CategoryFilter))
            RefreshFilteredTransactions();
    }

    void RefreshAll()
    {
        SelectedMonthDisplay = BudgetCalculator.FormatMonth(SelectedMonth);
        TransactionCountDetail = BudgetCalculator.FormatMonth(SelectedMonth);
        RefreshOptions();
        RefreshSummary();
        RefreshDashboard();
        RefreshFilteredTransactions();
        RefreshRecurring();
        RefreshCategories();
        RefreshSavingsGoals();
    }

    void RefreshOptions()
    {
        var categories = State.Budgets
            .Select(b => b.CategoryName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var expenseCategories = categories
            .Where(c => !c.Equals("Income", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var accounts = State.Accounts
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CategoryOptions = new ObservableCollection<string>(new[] { AllCategoriesFilter }.Concat(categories));
        ExpenseCategoryOptions = new ObservableCollection<string>(expenseCategories.Count > 0 ? expenseCategories : ["Other"]);
        AccountOptions = new ObservableCollection<string>(accounts.Count > 0 ? accounts : ["Checking"]);

        if (string.IsNullOrWhiteSpace(CategoryFilter) || !CategoryOptions.Any(c => c.Equals(CategoryFilter, StringComparison.OrdinalIgnoreCase)))
            CategoryFilter = AllCategoriesFilter;
        PurchaseCategory = EnsureOption(PurchaseCategory, ExpenseCategoryOptions);
        EditCategory = EnsureOption(EditCategory, ExpenseCategoryOptions);
        RecurringCategory = EnsureOption(RecurringCategory, ExpenseCategoryOptions);
        EditRecCategory = EnsureOption(EditRecCategory, ExpenseCategoryOptions);
        PurchaseAccount = EnsureOption(PurchaseAccount, AccountOptions);
        EditAccount = EnsureOption(EditAccount, AccountOptions);
        RecurringAccount = EnsureOption(RecurringAccount, AccountOptions);
        EditRecAccount = EnsureOption(EditRecAccount, AccountOptions);
        ImportAccount = EnsureOption(ImportAccount, AccountOptions);
    }

    void RefreshSummary()
    {
        var summary = BudgetCalculator.SummarizeMonth(State, SelectedMonth);

        IncomeDisplay = summary.Income.ToString("C2");
        IncomeDetail = summary.UsesPlannedIncome ? "Planned" : summary.ActualIncome > 0 ? "Actual" : "No income";
        ExpensesDisplay = summary.Expenses.ToString("C2");
        RecurringDisplay = $"{summary.Recurring:C2} recurring";
        CashFlowDisplay = summary.Remaining.ToString("C2");
        CashFlowDetail = $"{Math.Round(summary.SavingsRate)}% left";
        CashFlowPositive = summary.Remaining >= 0;
        TransactionCountDisplay = summary.Rows.Count.ToString();
    }

    void RefreshDashboard()
    {
        var trend = BudgetCalculator.TrendData(State, SelectedMonth);
        var labels = trend.Select(t => t.Month).ToArray();

        TrendXAxes = [new Axis { Labels = labels, TextSize = 12, SeparatorsPaint = new SolidColorPaint(SKColors.Transparent) }];
        TrendSeries =
        [
            new LineSeries<double>
            {
                Values = trend.Select(t => t.Income).ToArray(),
                Name = "Income",
                Stroke = new SolidColorPaint(SKColor.Parse("#3b82f6")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#3b82f6").WithAlpha(40)),
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#3b82f6")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#3b82f6")),
            },
            new LineSeries<double>
            {
                Values = trend.Select(t => t.Expenses).ToArray(),
                Name = "Expenses",
                Stroke = new SolidColorPaint(SKColor.Parse("#f97316")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#f97316").WithAlpha(40)),
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#f97316")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#f97316")),
            },
            new LineSeries<double>
            {
                Values = trend.Select(t => t.RecurringAmount).ToArray(),
                Name = "Recurring",
                Stroke = new SolidColorPaint(SKColor.Parse("#6366f1")) { StrokeThickness = 2 },
                Fill = new SolidColorPaint(SKColor.Parse("#6366f1").WithAlpha(40)),
                GeometrySize = 6,
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#6366f1")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#6366f1")),
            }
        ];

        var breakdown = BudgetCalculator.CategoryBreakdown(State, SelectedMonth);
        CategoryPieSeries = breakdown.Select(b => new PieSeries<double>
        {
            Values = [b.Value],
            Name = PieLegendLabel(b.Category, b.Value),
            Fill = ChartPaint(b.Color),
            Stroke = null,
            MaxRadialColumnWidth = 32,
            ToolTipLabelFormatter = point => PieLegendLabel(b.Category, point.Coordinate.PrimaryValue),
        } as ISeries).ToArray();

        var bars = BudgetCalculator.GetBudgetBars(State, SelectedMonth);
        BudgetBars = new ObservableCollection<BudgetBarItem>(bars);
        CategoryLimitsEnabled = State.CategoryLimitsEnabled;
        HasCategoryLimitValues = State.Budgets.Any(b => b.CategoryName != "Income" && b.MonthlyLimit > 0);

        RefreshYearlySummary();
    }

    void RefreshYearlySummary()
    {
        var yearly = BudgetCalculator.GetYearlySummary(State, SelectedMonth);
        YearlyPeriodLabel = yearly.PeriodLabel;
        YearlyIncome = yearly.Income.ToString("C2");
        YearlyExpenses = yearly.Expenses.ToString("C2");
        YearlyCashFlow = yearly.Remaining.ToString("C2");
        YearlyCashFlowValue = yearly.Remaining;
        YearlySaved = $"{Math.Round(yearly.SavingsRate)}%";
        YearlySavingsRateValue = yearly.SavingsRate;
        YearlyRecurring = yearly.Recurring.ToString("C2");
        YearlyAvgMonthly = yearly.AverageMonthlyExpenses.ToString("C2");
        YearlyBestMonth = yearly.BestMonthRemaining.ToString("C2");
        YearlyBestMonthLabel = $"in {yearly.BestMonthLabel}";
        YearlyHighestSpend = yearly.HighestExpenseMonthExpenses.ToString("C2");
        YearlyHighestSpendLabel = $"in {yearly.HighestExpenseMonthLabel}";
        YearlyActiveMonths = $"{yearly.ActiveMonthCount} active / {yearly.MonthCount} months";

        YearlyPieSeries = yearly.YearlyBreakdown.Select(b => new PieSeries<double>
        {
            Values = [b.Value],
            Name = PieLegendLabel(b.Category, b.Value),
            Fill = ChartPaint(b.Color),
            Stroke = null,
            MaxRadialColumnWidth = 34,
            ToolTipLabelFormatter = point => PieLegendLabel(b.Category, point.Coordinate.PrimaryValue),
        } as ISeries).ToArray();
    }

    void RefreshFilteredTransactions()
    {
        var allRows = BudgetCalculator.MonthTransactions(State, SelectedMonth);
        var query = (SearchQuery ?? "").Trim().ToLowerInvariant();

        var filtered = allRows.Where(t =>
        {
            if (query.Length > 0 && !$"{t.Merchant} {t.Category} {t.Account}".ToLowerInvariant().Contains(query))
                return false;
            if (!string.IsNullOrEmpty(CategoryFilter)
                && !CategoryFilter.Equals(AllCategoriesFilter, StringComparison.OrdinalIgnoreCase)
                && !t.Category.Equals(CategoryFilter, StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        });

        FilteredTransactions = new ObservableCollection<Transaction>(filtered);
    }

    void RefreshRecurring()
    {
        RecurringItems = new ObservableCollection<RecurringPurchase>(State.Recurring);
    }

    void RefreshCategories()
    {
        CategoryLimitBudgets = new ObservableCollection<BudgetCategory>(
            State.Budgets.Where(b => b.CategoryName != "Income"));
    }

    void RefreshSavingsGoals()
    {
        SavingsGoals = new ObservableCollection<SavingsGoal>(State.SavingsGoals);
    }

    void SaveAndRefresh()
    {
        if (ActiveProfile != null)
        {
            ActiveProfile.UpdatedAt = DateTime.UtcNow.ToString("o");
            _storage.SaveProfiles([.. Profiles]);
        }
        RefreshAll();
    }

    void ShowStatus(string message)
    {
        StatusMessage = message;
        _ = ClearStatusAfterDelay(message);
    }

    async Task ClearStatusAfterDelay(string message)
    {
        await Task.Delay(3000);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            if (StatusMessage == message) StatusMessage = "";
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            if (StatusMessage == message) StatusMessage = "";
        });
    }

    // ─── Navigation ───────────────────────────────────────────────────────

    [RelayCommand]
    void PreviousMonth()
    {
        SelectedMonth = BudgetCalculator.AddMonths(SelectedMonth, -1);
        PurchaseDate = $"{SelectedMonth}-15";
        RefreshAll();
    }

    [RelayCommand]
    void NextMonth()
    {
        SelectedMonth = BudgetCalculator.AddMonths(SelectedMonth, 1);
        PurchaseDate = $"{SelectedMonth}-15";
        RefreshAll();
    }

    [RelayCommand]
    void GoToCurrentMonth()
    {
        SelectedMonth = BudgetCalculator.CurrentMonthKey();
        PurchaseDate = $"{SelectedMonth}-15";
        RefreshAll();
    }

    [RelayCommand]
    void SelectTab(string tab) => SelectedTab = tab;

    // ─── Transactions ─────────────────────────────────────────────────────

    [RelayCommand]
    void AddPurchase()
    {
        if (string.IsNullOrWhiteSpace(PurchaseMerchant) || PurchaseAmount <= 0)
        {
            ShowStatus("Add a merchant and amount before saving.");
            return;
        }

        var transaction = new Transaction
        {
            Id = BudgetCalculator.MakeId(),
            Date = BudgetCalculator.IsISODate(PurchaseDate) ? PurchaseDate : $"{SelectedMonth}-15",
            Merchant = PurchaseMerchant.Trim(),
            Category = PurchaseType == TransactionType.Income ? "Income" : EnsureOption(PurchaseCategory, ExpenseCategoryOptions),
            Amount = Math.Round(PurchaseAmount, 2),
            Type = PurchaseType,
            Account = PurchaseAccount,
            Notes = string.IsNullOrWhiteSpace(PurchaseNotes) ? null : PurchaseNotes.Trim(),
            Source = TransactionSource.Manual
        };

        State.Transactions.Insert(0, transaction);
        PurchaseMerchant = "";
        PurchaseAmount = 0;
        PurchaseNotes = "";
        SaveAndRefresh();
        ShowStatus($"{transaction.Merchant} added.");
    }

    [RelayCommand]
    void StartEditTransaction(Transaction t)
    {
        if (t.Source == TransactionSource.Recurring)
        {
            var recurringId = MaterializedRecurringId(t);
            var recurring = State.Recurring.FirstOrDefault(r => r.Id == recurringId);
            if (recurring != null)
            {
                StartEditRecurring(recurring);
                SelectedTab = "Monthly";
                ShowStatus("Opened the matching monthly item.");
            }
            else
            {
                ShowStatus("Edit recurring entries from the Monthly tab.");
            }
            return;
        }

        EditingTransaction = t;
        EditDate = t.Date;
        EditMerchant = t.Merchant;
        EditAmount = t.Amount;
        EditCategory = t.Type == TransactionType.Income ? EnsureOption("", ExpenseCategoryOptions) : EnsureOption(t.Category, ExpenseCategoryOptions);
        EditType = t.Type;
        EditAccount = t.Account;
        EditNotes = t.Notes ?? "";
    }

    [RelayCommand]
    void SaveEditTransaction()
    {
        if (EditingTransaction == null || string.IsNullOrWhiteSpace(EditMerchant) || EditAmount <= 0)
        {
            ShowStatus("Add a merchant and amount before saving.");
            return;
        }

        EditingTransaction.Date = BudgetCalculator.IsISODate(EditDate) ? EditDate : EditingTransaction.Date;
        EditingTransaction.Merchant = EditMerchant.Trim();
        EditingTransaction.Amount = Math.Round(EditAmount, 2);
        EditingTransaction.Category = EditType == TransactionType.Income ? "Income" : EnsureOption(EditCategory, ExpenseCategoryOptions);
        EditingTransaction.Type = EditType;
        EditingTransaction.Account = EditAccount;
        EditingTransaction.Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim();

        EditingTransaction = null;
        SaveAndRefresh();
        ShowStatus("Entry updated.");
    }

    [RelayCommand]
    void CancelEditTransaction() => EditingTransaction = null;

    [RelayCommand]
    void RemoveTransaction(Transaction t)
    {
        if (t.Source == TransactionSource.Recurring)
        {
            ShowStatus("Recurring entries can be paused or deleted from the Monthly tab.");
            return;
        }

        State.Transactions.Remove(t);
        if (EditingTransaction?.Id == t.Id) EditingTransaction = null;
        SaveAndRefresh();
    }

    // ─── Recurring ────────────────────────────────────────────────────────

    [RelayCommand]
    void AddRecurring()
    {
        if (string.IsNullOrWhiteSpace(RecurringMerchant) || RecurringAmount <= 0)
        {
            ShowStatus("Add a name and amount.");
            return;
        }

        var startMonth = BudgetCalculator.IsMonthKey(RecurringStartMonth) ? RecurringStartMonth : SelectedMonth;
        var endMonth = BudgetCalculator.IsMonthKey(RecurringEndMonth) ? RecurringEndMonth : null;
        if (endMonth != null && string.Compare(endMonth, startMonth, StringComparison.Ordinal) < 0)
        {
            ShowStatus("End month must be after the start month.");
            return;
        }

        var item = new RecurringPurchase
        {
            Id = BudgetCalculator.MakeId(),
            Merchant = RecurringMerchant.Trim(),
            Category = RecurringType == TransactionType.Income ? "Income" : EnsureOption(RecurringCategory, ExpenseCategoryOptions),
            Amount = Math.Round(RecurringAmount, 2),
            Type = RecurringType,
            Day = Math.Clamp(RecurringDay, 1, 31),
            Account = RecurringAccount,
            Active = true,
            StartMonth = startMonth,
            EndMonth = endMonth,
            Notes = string.IsNullOrWhiteSpace(RecurringNotes) ? null : RecurringNotes.Trim()
        };

        State.Recurring.Insert(0, item);
        RecurringMerchant = "";
        RecurringAmount = 0;
        RecurringNotes = "";
        RecurringDay = 1;
        SaveAndRefresh();
        ShowStatus($"{item.Merchant} added as monthly {item.Type}.");
    }

    [RelayCommand]
    void ToggleRecurring(RecurringPurchase item)
    {
        item.Active = !item.Active;
        SaveAndRefresh();
    }

    [RelayCommand]
    void RemoveRecurring(RecurringPurchase item)
    {
        State.Recurring.Remove(item);
        if (EditingRecurring?.Id == item.Id) EditingRecurring = null;
        SaveAndRefresh();
    }

    [RelayCommand]
    void StartEditRecurring(RecurringPurchase item)
    {
        EditingRecurring = item;
        EditRecMerchant = item.Merchant;
        EditRecAmount = item.Amount;
        EditRecDay = item.Day;
        EditRecCategory = item.Type == TransactionType.Income ? EnsureOption("", ExpenseCategoryOptions) : EnsureOption(item.Category, ExpenseCategoryOptions);
        EditRecType = item.Type;
        EditRecAccount = item.Account;
        EditRecStartMonth = item.StartMonth;
        EditRecEndMonth = item.EndMonth ?? "";
        EditRecNotes = item.Notes ?? "";
    }

    [RelayCommand]
    void SaveEditRecurring()
    {
        if (EditingRecurring == null || string.IsNullOrWhiteSpace(EditRecMerchant) || EditRecAmount <= 0)
        {
            ShowStatus("Add a name and amount.");
            return;
        }

        var startMonth = BudgetCalculator.IsMonthKey(EditRecStartMonth) ? EditRecStartMonth : EditingRecurring.StartMonth;
        var endMonth = BudgetCalculator.IsMonthKey(EditRecEndMonth) ? EditRecEndMonth : null;
        if (endMonth != null && string.Compare(endMonth, startMonth, StringComparison.Ordinal) < 0)
        {
            ShowStatus("End month must be after the start month.");
            return;
        }

        EditingRecurring.Merchant = EditRecMerchant.Trim();
        EditingRecurring.Amount = Math.Round(EditRecAmount, 2);
        EditingRecurring.Day = Math.Clamp(EditRecDay, 1, 31);
        EditingRecurring.Category = EditRecType == TransactionType.Income ? "Income" : EnsureOption(EditRecCategory, ExpenseCategoryOptions);
        EditingRecurring.Type = EditRecType;
        EditingRecurring.Account = EditRecAccount;
        EditingRecurring.StartMonth = startMonth;
        EditingRecurring.EndMonth = endMonth;
        EditingRecurring.Notes = string.IsNullOrWhiteSpace(EditRecNotes) ? null : EditRecNotes.Trim();

        EditingRecurring = null;
        SaveAndRefresh();
        ShowStatus("Monthly item updated.");
    }

    [RelayCommand]
    void CancelEditRecurring() => EditingRecurring = null;

    // ─── Categories ───────────────────────────────────────────────────────

    [RelayCommand]
    void AddCategory()
    {
        var name = NewCategoryName.Trim();
        if (string.IsNullOrEmpty(name)) { ShowStatus("Enter a category name."); return; }
        if (name.Equals("Income", StringComparison.OrdinalIgnoreCase)) { ShowStatus("Income is reserved."); return; }
        if (name.Equals(AllCategoriesFilter, StringComparison.OrdinalIgnoreCase)) { ShowStatus("That category name is reserved."); return; }
        if (State.Budgets.Any(b => b.CategoryName.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { ShowStatus("Category already exists."); return; }

        var colors = new[] { "#6366f1", "#14b8a6", "#f97316", "#0891b2", "#ec4899", "#a855f7", "#ef4444", "#2563eb", "#6b7280" };
        State.Budgets.Add(new BudgetCategory
        {
            CategoryName = name,
            MonthlyLimit = Math.Max(0, Math.Round(NewCategoryLimit, 2)),
            Color = colors[State.Budgets.Count % colors.Length],
            Keywords = []
        });
        NewCategoryName = "";
        NewCategoryLimit = 0;
        SaveAndRefresh();
        ShowStatus($"{name} added.");
    }

    [RelayCommand]
    void RemoveCategory(BudgetCategory cat)
    {
        if (cat.CategoryName == "Income") return;
        var remaining = State.Budgets.Where(b => b.CategoryName != cat.CategoryName && b.CategoryName != "Income").ToList();
        if (remaining.Count == 0) { ShowStatus("Keep at least one expense category."); return; }

        var fallback = remaining[0].CategoryName;
        State.Budgets.RemoveAll(b => b.CategoryName == cat.CategoryName);
        foreach (var t in State.Transactions.Where(t => t.Type == TransactionType.Expense && t.Category == cat.CategoryName))
            t.Category = fallback;
        foreach (var r in State.Recurring.Where(r => r.Type == TransactionType.Expense && r.Category == cat.CategoryName))
            r.Category = fallback;

        SaveAndRefresh();
        ShowStatus($"{cat.CategoryName} removed.");
    }

    public void UpdateCategoryLimit(string categoryName, double limit)
    {
        var budget = State.Budgets.FirstOrDefault(b => b.CategoryName == categoryName);
        if (budget != null)
        {
            budget.MonthlyLimit = Math.Max(0, Math.Round(limit, 2));
            SaveAndRefresh();
        }
    }

    [RelayCommand]
    void ToggleCategoryLimits()
    {
        State.CategoryLimitsEnabled = !State.CategoryLimitsEnabled;
        SaveAndRefresh();
    }

    [RelayCommand]
    void ResetCategoryLimitsToZero()
    {
        foreach (var b in State.Budgets.Where(b => b.CategoryName != "Income"))
            b.MonthlyLimit = 0;
        SaveAndRefresh();
        ShowStatus("Category limits reset to $0.");
    }

    [RelayCommand]
    void RestoreCategoryDefaults()
    {
        foreach (var b in State.Budgets)
        {
            var def = SampleData.CategoryCatalog.FirstOrDefault(c => c.CategoryName == b.CategoryName);
            if (def != null) b.MonthlyLimit = def.MonthlyLimit;
        }
        SaveAndRefresh();
        ShowStatus("Default limits restored.");
    }

    // ─── Profiles ─────────────────────────────────────────────────────────

    [RelayCommand]
    void CreateProfile()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrEmpty(name)) { ShowStatus("Enter a profile name."); return; }
        if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { ShowStatus("Profile already exists."); return; }

        var state = ProfileTemplate switch
        {
            "sample" => SampleData.CreateInitialBudgetState(),
            "current" => CloneState(State),
            _ => SampleData.CreateEmptyBudgetState()
        };

        var profile = new BudgetProfile
        {
            Id = $"profile-{BudgetCalculator.MakeId()}",
            Name = name,
            State = state,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };

        Profiles.Add(profile);
        ActiveProfile = profile;
        RenameProfileName = profile.Name;
        NewProfileName = "";
        ProfileTemplate = "empty";
        SaveAndRefresh();
        ShowStatus($"{name} created.");
    }

    [RelayCommand]
    void RenameActiveProfile()
    {
        var name = RenameProfileName.Trim();
        if (string.IsNullOrEmpty(name) || ActiveProfile == null) return;
        if (Profiles.Any(p => p.Id != ActiveProfile.Id && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { ShowStatus("Profile name already taken."); return; }

        ActiveProfile.Name = name;
        SaveAndRefresh();
        ShowStatus($"Profile renamed to {name}.");
    }

    [RelayCommand]
    void DeleteActiveProfile()
    {
        if (Profiles.Count <= 1) { ShowStatus("Keep at least one profile."); return; }
        if (ActiveProfile == null) return;

        var deletedName = ActiveProfile.Name;
        Profiles.Remove(ActiveProfile);
        ActiveProfile = Profiles.FirstOrDefault();
        RenameProfileName = ActiveProfile?.Name ?? "";
        SaveAndRefresh();
        ShowStatus($"{deletedName} deleted.");
    }

    // ─── Import / Export ──────────────────────────────────────────────────

    [RelayCommand]
    void PreviewStatement()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".csv"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var text = System.IO.File.ReadAllText(dialog.FileName);
            var rows = CsvImporter.ParseStatement(text, ImportAccount, StatementType, State.Budgets);
            ImportPreview = new ObservableCollection<StatementRow>(rows);
            ShowStatus(rows.Count > 0 ? $"{rows.Count} rows parsed." : "No usable rows found.");
        }
        catch { ShowStatus("Failed to read the file."); }
    }

    [RelayCommand]
    void ImportRows()
    {
        if (ImportPreview.Count == 0) return;
        foreach (var row in ImportPreview)
        {
            State.Transactions.Insert(0, new Transaction
            {
                Id = BudgetCalculator.MakeId(),
                Date = row.Date,
                Merchant = row.Merchant,
                Category = row.Category,
                Amount = row.Amount,
                Type = row.Type,
                Account = row.Account,
                Notes = row.Notes,
                Source = TransactionSource.Import
            });
        }

        ShowStatus($"{ImportPreview.Count} rows imported.");
        ImportPreview.Clear();
        SaveAndRefresh();
    }

    [RelayCommand]
    void ExportData()
    {
        if (ActiveProfile == null) return;
        var result = _storage.ExportProfile(State, ActiveProfile.Name, SelectedMonth);
        if (!string.IsNullOrEmpty(result)) ShowStatus("Budget exported.");
    }

    [RelayCommand]
    void ImportData()
    {
        var imported = _storage.ImportProfile();
        if (imported != null && ActiveProfile != null)
        {
            ActiveProfile.State = imported;
            SaveAndRefresh();
            ShowStatus("Budget restored from file.");
        }
    }

    [RelayCommand]
    void ClearBudgetData()
    {
        if (ActiveProfile == null) return;
        if (MessageBox.Show("Clear all saved budget data from this profile?",
            "Clear data", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        ActiveProfile.State = SampleData.CreateEmptyBudgetState();
        ImportPreview.Clear();
        SearchQuery = "";
        CategoryFilter = AllCategoriesFilter;
        SaveAndRefresh();
        ShowStatus("Budget data cleared.");
    }

    [RelayCommand]
    void ResetSampleData()
    {
        if (ActiveProfile == null) return;
        if (MessageBox.Show("Reset this profile to sample data?",
            "Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        ActiveProfile.State = SampleData.CreateInitialBudgetState();
        SaveAndRefresh();
        ShowStatus("Sample data restored.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    static string EnsureOption(string value, IEnumerable<string> options)
    {
        var match = options.FirstOrDefault(o => o.Equals(value, StringComparison.OrdinalIgnoreCase));
        return match ?? options.First();
    }

    static SolidColorPaint ChartPaint(string color)
    {
        if (!string.IsNullOrWhiteSpace(color) && SKColor.TryParse(color, out var parsed))
            return new SolidColorPaint(parsed);
        return new SolidColorPaint(SKColor.Parse("#9ca3af"));
    }

    static string PieLegendLabel(string category, double value) =>
        $"{category}: {value.ToString("C2", CultureInfo.GetCultureInfo("en-US"))}";

    static string MaterializedRecurringId(Transaction transaction)
    {
        const string prefix = "recurring-";
        var month = BudgetCalculator.ToMonthKey(transaction.Date);
        var suffix = $"-{month}";

        if (!transaction.Id.StartsWith(prefix, StringComparison.Ordinal)
            || string.IsNullOrEmpty(month)
            || !transaction.Id.EndsWith(suffix, StringComparison.Ordinal))
            return "";

        return transaction.Id[prefix.Length..^suffix.Length];
    }

    static BudgetState CloneState(BudgetState source)
    {
        return new BudgetState
        {
            Transactions = source.Transactions.Select(t => new Transaction
            {
                Id = BudgetCalculator.MakeId(), Date = t.Date, Merchant = t.Merchant,
                Category = t.Category, Amount = t.Amount, Type = t.Type,
                Account = t.Account, Notes = t.Notes, Source = t.Source
            }).ToList(),
            Recurring = source.Recurring.Select(r => new RecurringPurchase
            {
                Id = BudgetCalculator.MakeId(), Merchant = r.Merchant, Category = r.Category,
                Amount = r.Amount, Type = r.Type, Day = r.Day, Account = r.Account,
                Active = r.Active, StartMonth = r.StartMonth, EndMonth = r.EndMonth, Notes = r.Notes
            }).ToList(),
            Budgets = source.Budgets.Select(b => new BudgetCategory
            {
                CategoryName = b.CategoryName, MonthlyLimit = b.MonthlyLimit,
                Color = b.Color, Keywords = [.. b.Keywords]
            }).ToList(),
            CategoryLimitsEnabled = source.CategoryLimitsEnabled,
            SavingsGoals = source.SavingsGoals.Select(g => new SavingsGoal
            {
                Id = BudgetCalculator.MakeId(), Name = g.Name, Target = g.Target,
                Saved = g.Saved, MonthlyContribution = g.MonthlyContribution, Color = g.Color
            }).ToList(),
            Accounts = [.. source.Accounts],
            PlannedMonthlyIncome = source.PlannedMonthlyIncome
        };
    }
}
