namespace OnlinePalengke.Admin.ViewModels;

/// <summary>
/// Static sample rows used to make the shell screens legible before the Application layer
/// exists. Nothing here touches a database, an HTTP client, or a repository — when the
/// real services land, each page swaps its <c>PlaceholderData.X</c> reference for an
/// injected query and this file is deleted.
/// </summary>
public static class PlaceholderData
{
    private static readonly DateTimeOffset Reference = DateTimeOffset.UtcNow;

    private static DateTimeOffset AgoMinutes(int minutes) => Reference.AddMinutes(-minutes);

    private static DateTimeOffset AgoHours(double hours) => Reference.AddHours(-hours);

    private static DateTimeOffset AgoDays(double days) => Reference.AddDays(-days);

    private static DateTimeOffset InMinutes(int minutes) => Reference.AddMinutes(minutes);

    // ---------------------------------------------------------------- dashboard

    public static IReadOnlyList<StatTileModel> DashboardTiles { get; } =
    [
        new("Orders today", "128", "across 6 markets", "primary"),
        new("Awaiting payment", "14", "quote accepted, unpaid", "warning"),
        new("Open quote windows", "3", "closing within the hour", "info"),
        new("Riders on shift", "22", "of 27 rostered", "success"),
    ];

    public static IReadOnlyList<OrderStatusCount> OrdersByStatus { get; } =
    [
        new("Collecting quotes", 19),
        new("Awaiting payment", 14),
        new("Paid", 41),
        new("Shopping", 23),
        new("Out for delivery", 18),
        new("Delivered", 11),
        new("Cancelled", 2),
    ];

    public static IReadOnlyList<OpenQuoteWindow> OpenQuoteWindows { get; } =
    [
        new("Balintawak Market", "7–9 AM", InMinutes(23), 34, 96),
        new("Marikina Public Market", "7–9 AM", InMinutes(41), 21, 58),
        new("Pasig Mega Market", "9–11 AM", InMinutes(118), 12, 17),
    ];

    public static IReadOnlyList<RiderOnShift> RidersOnShift { get; } =
    [
        new("Ramil Bautista", "Balintawak Market", "7–9 AM", 9, "Out for delivery"),
        new("Joan Sarmiento", "Marikina Public Market", "7–9 AM", 7, "Out for delivery"),
        new("Elmer Dizon", "Pasig Mega Market", "9–11 AM", 0, "On shift"),
        new("Kris Nolasco", "Balintawak Market", "9–11 AM", 4, "Shopping"),
    ];

    // ---------------------------------------------------------------- catalog

    /// <summary>Empty on purpose: the catalog is seeded per deployment.</summary>
    public static IReadOnlyList<CategoryRow> Categories { get; } = [];

    public static IReadOnlyList<ItemRow> Items { get; } =
    [
        new(1, "Bangus (milkfish)", "Fish & seafood", "kg", 0.450m, null, true),
        new(2, "Galunggong", "Fish & seafood", "kg", 0.120m, null, true),
        new(3, "Pork liempo", "Meat", "kg", 1.000m, null, true),
        new(4, "Chicken (whole)", "Meat", "pc", 1.200m, null, true),
        new(5, "Kalabasa", "Vegetables", "kg", 1.800m, null, true),
        new(6, "Sitaw", "Vegetables", "bundle", 0.250m, null, true),
        new(7, "Saba banana", "Fruit", "kg", 0.900m, null, false),
        new(8, "Red onion", "Vegetables", "kg", 0.110m, null, true),
    ];

    /// <summary>Empty on purpose: units are configured before the first market goes live.</summary>
    public static IReadOnlyList<UnitRow> Units { get; } = [];

    // ---------------------------------------------------------------- markets

    public static IReadOnlyList<MarketRow> Markets { get; } =
    [
        new(1, "Balintawak Market", "Quezon City", "Metro Manila", 48, 3, true, "Active"),
        new(2, "Marikina Public Market", "Marikina", "Metro Manila", 31, 2, true, "Active"),
        new(3, "Pasig Mega Market", "Pasig", "Metro Manila", 27, 2, false, "Active"),
        new(4, "Dagupan Fish Market", "Dagupan", "Pangasinan", 0, 0, false, "Onboarding"),
    ];

    public static MarketRow? FindMarket(int id) => Markets.FirstOrDefault(m => m.Id == id);

    public static IReadOnlyList<DeliveryWindowRow> DeliveryWindowsFor(int marketId) => marketId switch
    {
        1 =>
        [
            new(11, "7–9 AM", new TimeOnly(7, 0), new TimeOnly(9, 0), 90, 60, true),
            new(12, "9–11 AM", new TimeOnly(9, 0), new TimeOnly(11, 0), 90, 40, true),
            new(13, "4–6 PM", new TimeOnly(16, 0), new TimeOnly(18, 0), 120, 25, false),
        ],
        2 =>
        [
            new(21, "7–9 AM", new TimeOnly(7, 0), new TimeOnly(9, 0), 90, 45, true),
            new(22, "9–11 AM", new TimeOnly(9, 0), new TimeOnly(11, 0), 90, 30, true),
        ],
        3 =>
        [
            new(31, "9–11 AM", new TimeOnly(9, 0), new TimeOnly(11, 0), 60, 30, true),
            new(32, "1–3 PM", new TimeOnly(13, 0), new TimeOnly(15, 0), 60, 20, true),
        ],
        _ => [],
    };

    // ---------------------------------------------------------------- verification

    public static IReadOnlyList<VerificationRow> VerificationQueue { get; } =
    [
        new(1, "Aling Nena's Gulayan", "Partner", "DTI registration", "Balintawak Market", AgoHours(2.5), new DateOnly(2027, 3, 14), "Pending"),
        new(2, "Aling Nena's Gulayan", "Partner", "Stall lease contract", "Balintawak Market", AgoHours(2.5), new DateOnly(2026, 12, 31), "Pending"),
        new(3, "Ramil Bautista", "Rider", "Driver's licence", "Balintawak Market", AgoHours(6), new DateOnly(2029, 5, 2), "In review"),
        new(4, "Joan Sarmiento", "Rider", "OR / CR", "Marikina Public Market", AgoDays(1.2), new DateOnly(2026, 9, 30), "In review"),
        new(5, "Kuya Boy Karne", "Partner", "Sanitary permit", "Marikina Public Market", AgoDays(2.4), null, "Rejected"),
        new(6, "Elmer Dizon", "Rider", "NBI clearance", "Pasig Mega Market", AgoDays(3.1), new DateOnly(2027, 1, 18), "Approved"),
        new(7, "Sea Fresh Isda", "Partner", "BIR certificate", "Pasig Mega Market", AgoDays(4.6), new DateOnly(2026, 8, 20), "Expiring soon"),
    ];

    public static IReadOnlyList<FilterChip> VerificationFilters { get; } =
    [
        new("all", "All", 7),
        new("pending", "Pending", 2),
        new("in-review", "In review", 2),
        new("approved", "Approved", 1),
        new("rejected", "Rejected", 1),
        new("expiring", "Expiring soon", 1),
    ];

    // ---------------------------------------------------------------- content

    public static IReadOnlyList<ContentPostRow> ContentFeed { get; } =
    [
        new(1, "Aling Nena's Gulayan", "Balintawak Market", "Fresh kangkong, harvested 4 AM",
            "Straight from Bulacan farms every morning. Ask for a bundle discount when you order three or more.",
            AgoMinutes(35), 0, "Live"),
        new(2, "Kuya Boy Karne", "Marikina Public Market", "Liempo cuts for the weekend",
            "Thick cut, hand trimmed. We reserve stock for orders placed before the 7 AM window closes.",
            AgoHours(4), 2, "Reported"),
        new(3, "Sea Fresh Isda", "Pasig Mega Market", "Bangus deboned free of charge",
            "Every order over two kilos gets free deboning. Packed on ice for the morning run.",
            AgoHours(9), 0, "Live"),
        new(4, "Lola Cita Prutas", "Balintawak Market", "Mango season is here",
            "Carabao mangoes, tree ripened. Limited crates each morning.",
            AgoDays(1.4), 5, "Taken down"),
    ];

    public static IReadOnlyList<FilterChip> ContentFilters { get; } =
    [
        new("all", "All posts", 4),
        new("reported", "Reported", 1),
        new("taken-down", "Taken down", 1),
        new("live", "Live", 2),
    ];

    // ---------------------------------------------------------------- orders

    public static IReadOnlyList<OrderRow> Orders { get; } =
    [
        new("OP-24081-0417", "Maricel Ocampo", "Balintawak Market", "7–9 AM", new DateOnly(2026, 8, 14), AgoHours(11), 12, 1_842.50m, "Out for delivery"),
        new("OP-24081-0418", "Dennis Villamor", "Balintawak Market", "7–9 AM", new DateOnly(2026, 8, 14), AgoHours(10.5), 7, 963.00m, "Shopping"),
        new("OP-24081-0421", "Grace Tolentino", "Marikina Public Market", "9–11 AM", new DateOnly(2026, 8, 14), AgoHours(9), 18, 3_215.75m, "Paid"),
        new("OP-24081-0424", "Rowena Balmes", "Pasig Mega Market", "9–11 AM", new DateOnly(2026, 8, 14), AgoHours(6.2), 5, 604.25m, "Awaiting payment"),
        new("OP-24081-0426", "Jomar Alcantara", "Marikina Public Market", "7–9 AM", new DateOnly(2026, 8, 15), AgoHours(3.4), 9, 0m, "Collecting quotes"),
        new("OP-24080-0388", "Liza Mercado", "Balintawak Market", "7–9 AM", new DateOnly(2026, 8, 13), AgoDays(1.5), 14, 2_108.00m, "Delivered"),
        new("OP-24080-0371", "Arnel Pascual", "Pasig Mega Market", "1–3 PM", new DateOnly(2026, 8, 13), AgoDays(1.9), 6, 0m, "Cancelled"),
    ];

    public static OrderRow? FindOrder(string reference) =>
        Orders.FirstOrDefault(o => string.Equals(o.Reference, reference, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<FilterChip> OrderFilters { get; } =
    [
        new("all", "All", 7),
        new("collecting-quotes", "Collecting quotes", 1),
        new("awaiting-payment", "Awaiting payment", 1),
        new("paid", "Paid", 1),
        new("shopping", "Shopping", 1),
        new("out-for-delivery", "Out for delivery", 1),
        new("delivered", "Delivered", 1),
        new("cancelled", "Cancelled", 1),
    ];

    public static IReadOnlyList<OrderLineRow> OrderLines { get; } =
    [
        new("Bangus (milkfish)", "Fish & seafood", 2.0m, "kg", "Sea Fresh Isda", 220.00m, 440.00m),
        new("Galunggong", "Fish & seafood", 1.5m, "kg", "Sea Fresh Isda", 180.00m, 270.00m),
        new("Pork liempo", "Meat", 1.0m, "kg", "Kuya Boy Karne", 385.00m, 385.00m),
        new("Kalabasa", "Vegetables", 1.0m, "kg", "Aling Nena's Gulayan", 60.00m, 60.00m),
        new("Sitaw", "Vegetables", 3.0m, "bundle", "Aling Nena's Gulayan", 25.00m, 75.00m),
        new("Red onion", "Vegetables", 0.5m, "kg", "Aling Nena's Gulayan", 190.00m, 95.00m),
    ];

    // ---------------------------------------------------------------- delivery runs

    /// <summary>Empty on purpose: runs are assembled per market, window and date.</summary>
    public static IReadOnlyList<DeliveryRunRow> DeliveryRuns { get; } = [];

    public static IReadOnlyList<string> AvailableRiders { get; } =
    [
        "Ramil Bautista",
        "Joan Sarmiento",
        "Elmer Dizon",
        "Kris Nolasco",
    ];

    // ---------------------------------------------------------------- payouts

    public static IReadOnlyList<PayoutBalanceRow> PayoutBalances { get; } =
    [
        new(1, "Aling Nena's Gulayan", "Partner", "Balintawak Market", 23, 18_420.00m, 1_473.60m, 16_946.40m, AgoDays(6)),
        new(2, "Kuya Boy Karne", "Partner", "Marikina Public Market", 17, 26_310.50m, 2_104.84m, 24_205.66m, AgoDays(6)),
        new(3, "Sea Fresh Isda", "Partner", "Pasig Mega Market", 11, 14_075.00m, 1_126.00m, 12_949.00m, AgoDays(4)),
        new(4, "Ramil Bautista", "Rider", "Balintawak Market", 58, 6_960.00m, 1_044.00m, 5_916.00m, AgoDays(6)),
        new(5, "Joan Sarmiento", "Rider", "Marikina Public Market", 41, 4_920.00m, 738.00m, 4_182.00m, AgoDays(5)),
    ];

    public static IReadOnlyList<PayoutBatchRow> PayoutBatches { get; } =
    [
        new("PB-2026-W32", new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 9), 14, 71_204.18m, "BDO-8841203", AgoDays(4), "Paid"),
        new("PB-2026-W31", new DateOnly(2026, 7, 27), new DateOnly(2026, 8, 2), 12, 63_880.05m, "BDO-8829117", AgoDays(11), "Paid"),
    ];

    // ---------------------------------------------------------------- audit

    public static IReadOnlyList<AuditEntryRow> AuditEntries { get; } =
    [
        new(9014, AgoMinutes(12), "ops.wilson", "Administrator", "content.takedown", "ContentPost", "#4", "Took down \"Mango season is here\" after 5 reports."),
        new(9013, AgoMinutes(48), "ops.wilson", "Administrator", "kyc.approve", "RiderDocument", "#6", "Approved NBI clearance for Elmer Dizon."),
        new(9012, AgoHours(2.1), "ops.mae", "Operations", "run.assign", "DeliveryRun", "DR-24081-03", "Assigned Ramil Bautista to Balintawak 7–9 AM."),
        new(9011, AgoHours(5.4), "system", "System", "quote.window.close", "QuoteWindow", "QW-24081-11", "Closed quote window; 96 quotes received."),
        new(9010, AgoHours(7.8), "ops.mae", "Operations", "config.update", "PlatformSettings", "delivery-fee", "Changed per-km rate from ₱11.00 to ₱12.00."),
        new(9009, AgoDays(1.1), "ops.wilson", "Administrator", "kyc.reject", "PartnerDocument", "#5", "Rejected sanitary permit: document illegible."),
        new(9008, AgoDays(1.6), "ops.wilson", "Administrator", "market.publish", "Market", "#3", "Published Pasig Mega Market."),
    ];

    public static IReadOnlyList<FilterChip> AuditActorFilters { get; } =
    [
        new("all", "All actors"),
        new("administrator", "Administrator"),
        new("operations", "Operations"),
        new("system", "System"),
    ];
}
