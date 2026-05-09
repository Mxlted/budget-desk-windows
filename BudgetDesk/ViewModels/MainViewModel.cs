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
using System.Windows.Media;

namespace BudgetDesk.ViewModels;

public sealed record ThemeOption(InterfaceTheme Mode, string Name);

public partial class MainViewModel : ObservableObject
{
    const string AllCategoriesFilter = "All categories";

    readonly StorageService _storage = new();
    readonly BudgetState _fallbackState = SampleData.CreateInitialBudgetState();
    bool _initialized;
    bool _syncingTheme;

    [ObservableProperty] string _selectedTab = "Dashboard";
    [ObservableProperty] string _selectedMonth = BudgetCalculator.CurrentMonthKey();
    [ObservableProperty] string _selectedMonthDisplay = "";
    [ObservableProperty] string _statusMessage = "";

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(InterfaceTheme.Dark, "Dark"),
        new(InterfaceTheme.Light, "Light"),
        new(InterfaceTheme.Oled, "OLED dark")
    ];

    [ObservableProperty] InterfaceTheme _selectedTheme = InterfaceTheme.Dark;

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
    [ObservableProperty] int _chartThemeVersion;
    [ObservableProperty] ObservableCollection<BudgetBarItem> _budgetBars = [];
    [ObservableProperty] ObservableCollection<SavingsGoal> _savingsGoals = [];
    [ObservableProperty] bool _categoryLimitsEnabled = true;
    [ObservableProperty] bool _hasCategoryLimitValues = true;
    [ObservableProperty] bool _showSavingsGoalLimits = true;
    [ObservableProperty] string _savingsGoalsTitle = "Savings goals";
    [ObservableProperty] double _maxSavingsAmount = 1;

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

    [ObservableProperty] SolidColorPaint _chartLegendTextPaint = new(SKColor.Parse("#f5f5f5"));
    [ObservableProperty] SolidColorPaint _chartTooltipTextPaint = new(SKColor.Parse("#111827"));
    [ObservableProperty] SolidColorPaint _chartTooltipBackgroundPaint = new(SKColor.Parse("#ffffff"));

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

        // Restore the profile that was active the last time the app closed.
        // Fall back to the first profile if the saved id isn't in the list
        // (e.g. it was deleted, or this is a fresh install).
        var lastId = _storage.LoadActiveProfileId();
        ActiveProfile = (string.IsNullOrWhiteSpace(lastId)
            ? null
            : Profiles.FirstOrDefault(p => string.Equals(p.Id, lastId, StringComparison.OrdinalIgnoreCase)))
            ?? Profiles.FirstOrDefault();
        RenameProfileName = ActiveProfile?.Name ?? "";
        SelectedMonth = BudgetCalculator.CurrentMonthKey();
        RecurringStartMonth = SelectedMonth;
        PurchaseDate = $"{SelectedMonth}-15";
        SelectedTheme = State.Theme;
        ApplyTheme(SelectedTheme);

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
        SyncThemeFromState();
        RefreshAll();

        // Remember which profile the user is on so the next launch reopens it.
        _storage.SaveActiveProfileId(value?.Id ?? "");
    }

    partial void OnSelectedThemeChanged(InterfaceTheme value)
    {
        ApplyTheme(value);

        if (_initialized)
        {
            RefreshAll();
            ChartThemeVersion++;
        }
        else
        {
            RefreshDashboard();
        }

        if (!_initialized || _syncingTheme) return;

        State.Theme = value;
        SaveProfilesOnly();
        ShowStatus($"Theme set to {ThemeName(value)}.");
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

        TrendXAxes = [new Axis
        {
            Labels = labels,
            TextSize = 12,
            LabelsPaint = ChartLegendTextPaint,
            SeparatorsPaint = new SolidColorPaint(SKColors.Transparent)
        }];
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
        _suppressCategoryLimitsSync = true;
        try { CategoryLimitsEnabled = State.CategoryLimitsEnabled; }
        finally { _suppressCategoryLimitsSync = false; }
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

        // Goal "limits" (the target + progress bar) only make sense when category
        // limits are on AND at least one goal actually has a Target > 0. When they
        // don't, hide the target/progress bits and rename the card to "Savings".
        var hasTargets = State.SavingsGoals.Any(g => g.Target > 0);
        ShowSavingsGoalLimits = State.CategoryLimitsEnabled && hasTargets;
        SavingsGoalsTitle = ShowSavingsGoalLimits ? "Savings goals" : "Savings";

        // Denominator for the no-target bar. Must be > 0 so GoalPercentConverter
        // doesn't fall through to 0; if every goal is at $0 we keep 1 and bars
        // collapse to empty, which is the right visual.
        var max = State.SavingsGoals.Count == 0 ? 0 : State.SavingsGoals.Max(g => g.Saved);
        MaxSavingsAmount = max > 0 ? max : 1;
    }

    void SaveAndRefresh()
    {
        SaveProfilesOnly();
        RefreshAll();
    }

    void SaveProfilesOnly()
    {
        if (ActiveProfile != null)
        {
            ActiveProfile.UpdatedAt = DateTime.UtcNow.ToString("o");
            _storage.SaveProfiles([.. Profiles]);
        }
    }

    void SyncThemeFromState()
    {
        _syncingTheme = true;
        try
        {
            SelectedTheme = State.Theme;
            ApplyTheme(SelectedTheme);
        }
        finally
        {
            _syncingTheme = false;
        }
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

    bool _suppressCategoryLimitsSync;

    partial void OnCategoryLimitsEnabledChanged(bool value)
    {
        if (!_initialized || _suppressCategoryLimitsSync) return;
        if (State.CategoryLimitsEnabled == value) return;

        State.CategoryLimitsEnabled = value;
        SaveProfilesOnly();
        RefreshDashboard();
        RefreshSavingsGoals();
        ShowStatus(value ? "Category limits enabled." : "Category limits disabled.");
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
            SyncThemeFromState();
            SaveAndRefresh();
            ShowStatus("Budget restored from file.");
        }
    }

    [RelayCommand]
    void ClearBudgetData()
    {
        if (ActiveProfile == null) return;
        var confirmed = Views.ConfirmDialog.Show(
            owner: Application.Current?.MainWindow,
            title: "Remove all budget data",
            body: $"This permanently removes every transaction, recurring item, savings goal, " +
                  $"and category limit from the \"{ActiveProfile.Name}\" profile. Theme and " +
                  $"profile name are kept. This cannot be undone.",
            confirmLabel: "REMOVE ALL BUDGET DATA",
            backupHint: "Tip: export this profile from \"Local storage\" first if you might want it back.",
            destructive: true);
        if (!confirmed) return;

        ActiveProfile.State = SampleData.CreateEmptyBudgetState();
        ActiveProfile.State.Theme = SelectedTheme;
        ImportPreview.Clear();
        SearchQuery = "";
        CategoryFilter = AllCategoriesFilter;
        SaveAndRefresh();
        ShowStatus("Budget data removed.");
    }

    [RelayCommand]
    void ResetSampleData()
    {
        if (ActiveProfile == null) return;
        var confirmed = Views.ConfirmDialog.Show(
            owner: Application.Current?.MainWindow,
            title: "Restore default sample data",
            body: $"This will replace everything in the \"{ActiveProfile.Name}\" profile with the " +
                  $"built-in sample budget. All of your existing transactions, recurring items, " +
                  $"savings goals, and category limits will be removed.",
            confirmLabel: "Restore Default Sample Data",
            backupHint: "Make sure to back up first — export this profile from \"Local storage\" if you want a copy of the current data.",
            destructive: true);
        if (!confirmed) return;

        ActiveProfile.State = SampleData.CreateInitialBudgetState();
        ActiveProfile.State.Theme = SelectedTheme;
        SaveAndRefresh();
        ShowStatus("Sample data restored.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    void ApplyTheme(InterfaceTheme theme)
    {
        var palette = PaletteFor(theme);

        SetBrush("PageBgBrush", palette.PageBg);
        SetBrush("CardBgBrush", palette.CardBg);
        SetBrush("CardBorderBrush", palette.CardBorder);
        SetBrush("HeaderBgBrush", palette.HeaderBg);
        SetBrush("TextPrimaryBrush", palette.TextPrimary);
        SetBrush("TextSecondaryBrush", palette.TextSecondary);
        SetBrush("TextDimmedBrush", palette.TextDimmed);
        SetBrush("AccentBlueBrush", palette.AccentBlue);
        SetBrush("AccentOrangeBrush", palette.AccentOrange);
        SetBrush("AccentGreenBrush", palette.AccentGreen);
        SetBrush("AccentRedBrush", palette.AccentRed);
        SetBrush("ButtonBgBrush", palette.ButtonBg);
        SetBrush("ButtonHoverBrush", palette.ButtonHover);
        SetBrush("ButtonPressedBrush", palette.ButtonPressed);
        SetBrush("InputBgBrush", palette.InputBg);
        SetBrush("InputBorderBrush", palette.InputBorder);
        SetBrush("InputHoverBgBrush", palette.InputHoverBg);
        SetBrush("InputHoverBorderBrush", palette.InputHoverBorder);
        SetBrush("ComboItemHoverBrush", palette.ComboItemHover);
        SetBrush("ComboItemSelectedBgBrush", palette.ComboItemSelected);
        SetBrush("TabActiveBgBrush", palette.AccentBlue);
        SetBrush("TabInactiveBgBrush", palette.TabInactive);
        SetBrush("AccentBlueHoverBrush", palette.AccentBlueHover);
        SetBrush("AccentBluePressedBrush", palette.AccentBluePressed);
        SetBrush("AccentRedHoverBrush", palette.AccentRedHover);
        SetBrush("AccentRedPressedBrush", palette.AccentRedPressed);
        SetBrush("PositiveBadgeBgBrush", palette.PositiveBadgeBg);
        SetBrush("WarningBadgeBgBrush", palette.WarningBadgeBg);
        SetBrush("NegativeBadgeBgBrush", palette.NegativeBadgeBg);
        SetBrush("SuccessStatusBgBrush", palette.SuccessStatusBg);
        SetBrush("SuccessStatusBorderBrush", palette.SuccessStatusBorder);
        SetBrush("SubtlePanelBgBrush", palette.SubtlePanelBg);
        SetBrush("DataGridHeaderBgBrush", palette.DataGridHeaderBg);
        SetBrush("DataGridAlternateRowBgBrush", palette.DataGridAlternateRowBg);
        SetBrush("DataGridCellSelectedBgBrush", palette.DataGridCellSelectedBg);
        SetBrush("DataGridRowHoverBgBrush", palette.DataGridRowHoverBg);
        SetBrush("DataGridRowSelectedBgBrush", palette.DataGridRowSelectedBg);
        SetBrush("ProgressTrackBrush", palette.ProgressTrack);

        SetBrush(SystemColors.WindowBrushKey, palette.InputBg);
        SetBrush(SystemColors.ControlBrushKey, palette.InputBg);
        SetBrush(SystemColors.ControlTextBrushKey, palette.TextPrimary);
        SetBrush(SystemColors.HighlightBrushKey, palette.ComboItemHover);
        SetBrush(SystemColors.HighlightTextBrushKey, palette.TextPrimary);

        ChartLegendTextPaint = new SolidColorPaint(SKColor.Parse(palette.ChartText));
        // Keep chart tooltips stable across runtime theme switches. LiveCharts can
        // keep the previous tooltip visual alive briefly, so theme-dependent
        // tooltip paints can become white-on-white or black-on-black.
        ChartTooltipTextPaint = new SolidColorPaint(SKColor.Parse("#111827"));
        ChartTooltipBackgroundPaint = new SolidColorPaint(SKColor.Parse("#ffffff"));
    }

    static void SetBrush(object key, string color)
    {
        if (Application.Current?.Resources == null) return;
        SetBrush(Application.Current.Resources, key, WpfColor(color));
    }

    static bool SetBrush(ResourceDictionary dictionary, object key, Color color)
    {
        if (dictionary.Contains(key))
        {
            dictionary[key] = new SolidColorBrush(color);
            return true;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (SetBrush(merged, key, color)) return true;
        }

        return false;
    }

    static Color WpfColor(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;

    static string ThemeName(InterfaceTheme theme) => theme switch
    {
        InterfaceTheme.Light => "Light",
        InterfaceTheme.Oled => "OLED dark",
        _ => "Dark"
    };

    static ThemePalette PaletteFor(InterfaceTheme theme) => theme switch
    {
        InterfaceTheme.Light => new ThemePalette
        {
            PageBg = "#f3f6fb",
            CardBg = "#ffffff",
            CardBorder = "#d8dee8",
            HeaderBg = "#ffffff",
            TextPrimary = "#111827",
            TextSecondary = "#4b5563",
            TextDimmed = "#6b7280",
            AccentBlue = "#2563eb",
            AccentBlueHover = "#1d4ed8",
            AccentBluePressed = "#1e40af",
            AccentOrange = "#ea580c",
            AccentGreen = "#0f766e",
            AccentRed = "#dc2626",
            AccentRedHover = "#b91c1c",
            AccentRedPressed = "#991b1b",
            ButtonBg = "#eef2f7",
            ButtonHover = "#e2e8f0",
            ButtonPressed = "#cbd5e1",
            InputBg = "#ffffff",
            InputBorder = "#cbd5e1",
            InputHoverBg = "#f8fafc",
            InputHoverBorder = "#94a3b8",
            ComboItemHover = "#eaf2ff",
            ComboItemSelected = "#dbeafe",
            TabInactive = "#e8edf5",
            PositiveBadgeBg = "#dbeafe",
            WarningBadgeBg = "#ffedd5",
            NegativeBadgeBg = "#fee2e2",
            SuccessStatusBg = "#dcfce7",
            SuccessStatusBorder = "#86efac",
            SubtlePanelBg = "#f8fafc",
            DataGridHeaderBg = "#f1f5f9",
            DataGridAlternateRowBg = "#f8fafc",
            DataGridCellSelectedBg = "#dbeafe",
            DataGridRowHoverBg = "#eff6ff",
            DataGridRowSelectedBg = "#dbeafe",
            ProgressTrack = "#e5e7eb",
            ChartText = "#111827"
        },
        InterfaceTheme.Oled => new ThemePalette
        {
            PageBg = "#000000",
            CardBg = "#050505",
            CardBorder = "#1f1f1f",
            HeaderBg = "#000000",
            TextPrimary = "#f8fafc",
            TextSecondary = "#a1a1aa",
            TextDimmed = "#71717a",
            AccentBlue = "#3b82f6",
            AccentBlueHover = "#2563eb",
            AccentBluePressed = "#1d4ed8",
            AccentOrange = "#fb923c",
            AccentGreen = "#2dd4bf",
            AccentRed = "#f87171",
            AccentRedHover = "#ef4444",
            AccentRedPressed = "#dc2626",
            ButtonBg = "#101010",
            ButtonHover = "#1a1a1a",
            ButtonPressed = "#222222",
            InputBg = "#080808",
            InputBorder = "#242424",
            InputHoverBg = "#101010",
            InputHoverBorder = "#3a3a3a",
            ComboItemHover = "#171717",
            ComboItemSelected = "#0b2447",
            TabInactive = "#101010",
            PositiveBadgeBg = "#071d36",
            WarningBadgeBg = "#2a1605",
            NegativeBadgeBg = "#2a0909",
            SuccessStatusBg = "#031b16",
            SuccessStatusBorder = "#0f4d42",
            SubtlePanelBg = "#080808",
            DataGridHeaderBg = "#080808",
            DataGridAlternateRowBg = "#050505",
            DataGridCellSelectedBg = "#111111",
            DataGridRowHoverBg = "#0d0d0d",
            DataGridRowSelectedBg = "#101010",
            ProgressTrack = "#111111",
            ChartText = "#f8fafc"
        },
        _ => new ThemePalette
        {
            PageBg = "#121212",
            CardBg = "#1e1e1e",
            CardBorder = "#2a2a2a",
            HeaderBg = "#181818",
            TextPrimary = "#f5f5f5",
            TextSecondary = "#9ca3af",
            TextDimmed = "#6b7280",
            AccentBlue = "#3b82f6",
            AccentBlueHover = "#2563eb",
            AccentBluePressed = "#1d4ed8",
            AccentOrange = "#f97316",
            AccentGreen = "#14b8a6",
            AccentRed = "#ef4444",
            AccentRedHover = "#dc2626",
            AccentRedPressed = "#b91c1c",
            ButtonBg = "#2a2a2a",
            ButtonHover = "#333333",
            ButtonPressed = "#3b3b3b",
            InputBg = "#252525",
            InputBorder = "#3a3a3a",
            InputHoverBg = "#2f2f2f",
            InputHoverBorder = "#4a4a4a",
            ComboItemHover = "#333333",
            ComboItemSelected = "#1e3a5f",
            TabInactive = "#252525",
            PositiveBadgeBg = "#1e3a5f",
            WarningBadgeBg = "#3b2510",
            NegativeBadgeBg = "#3b1515",
            SuccessStatusBg = "#122b26",
            SuccessStatusBorder = "#1f5f52",
            SubtlePanelBg = "#1a1a1a",
            DataGridHeaderBg = "#252525",
            DataGridAlternateRowBg = "#1a1a1a",
            DataGridCellSelectedBg = "#2a2a2a",
            DataGridRowHoverBg = "#222222",
            DataGridRowSelectedBg = "#252525",
            ProgressTrack = "#252525",
            ChartText = "#f5f5f5"
        }
    };

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
            PlannedMonthlyIncome = source.PlannedMonthlyIncome,
            Theme = source.Theme
        };
    }
}

sealed class ThemePalette
{
    public string PageBg { get; init; } = "";
    public string CardBg { get; init; } = "";
    public string CardBorder { get; init; } = "";
    public string HeaderBg { get; init; } = "";
    public string TextPrimary { get; init; } = "";
    public string TextSecondary { get; init; } = "";
    public string TextDimmed { get; init; } = "";
    public string AccentBlue { get; init; } = "";
    public string AccentBlueHover { get; init; } = "";
    public string AccentBluePressed { get; init; } = "";
    public string AccentOrange { get; init; } = "";
    public string AccentGreen { get; init; } = "";
    public string AccentRed { get; init; } = "";
    public string AccentRedHover { get; init; } = "";
    public string AccentRedPressed { get; init; } = "";
    public string ButtonBg { get; init; } = "";
    public string ButtonHover { get; init; } = "";
    public string ButtonPressed { get; init; } = "";
    public string InputBg { get; init; } = "";
    public string InputBorder { get; init; } = "";
    public string InputHoverBg { get; init; } = "";
    public string InputHoverBorder { get; init; } = "";
    public string ComboItemHover { get; init; } = "";
    public string ComboItemSelected { get; init; } = "";
    public string TabInactive { get; init; } = "";
    public string PositiveBadgeBg { get; init; } = "";
    public string WarningBadgeBg { get; init; } = "";
    public string NegativeBadgeBg { get; init; } = "";
    public string SuccessStatusBg { get; init; } = "";
    public string SuccessStatusBorder { get; init; } = "";
    public string SubtlePanelBg { get; init; } = "";
    public string DataGridHeaderBg { get; init; } = "";
    public string DataGridAlternateRowBg { get; init; } = "";
    public string DataGridCellSelectedBg { get; init; } = "";
    public string DataGridRowHoverBg { get; init; } = "";
    public string DataGridRowSelectedBg { get; init; } = "";
    public string ProgressTrack { get; init; } = "";
    public string ChartText { get; init; } = "";
}
