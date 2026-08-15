/// Wire DTOs for `POST /api/customer/markets/eligibility`.
///
/// Mirrors `OnlinePalengke.Application.Markets.MarketContracts`' `EligibleMarketResponse`
/// / `EligibilityResponse` — kept local to this app rather than in `palengke_core` because
/// no other app calls this endpoint.
class EligibleMarket {
  const EligibleMarket({
    required this.id,
    required this.name,
    required this.city,
    required this.province,
  });

  factory EligibleMarket.fromJson(Map<String, dynamic> json) => EligibleMarket(
        id: json['id'] as int,
        name: json['name'] as String,
        city: json['city'] as String,
        province: json['province'] as String,
      );

  final int id;
  final String name;
  final String city;
  final String province;
}

/// [message] is always ready to display, per decision 14 — refusal comes with a clear
/// explanation, not just an empty [markets] list the UI has to interpret on its own.
class EligibilityResult {
  const EligibilityResult({
    required this.isEligible,
    required this.markets,
    required this.message,
  });

  factory EligibilityResult.fromJson(Map<String, dynamic> json) => EligibilityResult(
        isEligible: json['isEligible'] as bool,
        markets: (json['markets'] as List<dynamic>)
            .map((m) => EligibleMarket.fromJson(m as Map<String, dynamic>))
            .toList(),
        message: json['message'] as String,
      );

  final bool isEligible;
  final List<EligibleMarket> markets;
  final String message;
}
