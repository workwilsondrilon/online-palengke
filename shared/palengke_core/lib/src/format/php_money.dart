import 'package:intl/intl.dart';

/// Philippine peso formatting.
///
/// Money crossing the API is minor units (centavos) as an integer wherever
/// possible — doubles and money do not mix — so both entry points are
/// provided and [fromCentavos] is the one to prefer.
abstract final class PhpMoney {
  static final NumberFormat _peso = NumberFormat.currency(
    locale: 'en_PH',
    symbol: '₱',
    decimalDigits: 2,
  );

  static final NumberFormat _pesoWhole = NumberFormat.currency(
    locale: 'en_PH',
    symbol: '₱',
    decimalDigits: 0,
  );

  /// `1234.5` -> `₱1,234.50`
  static String format(num amount) => _peso.format(amount);

  /// `123450` -> `₱1,234.50`
  static String fromCentavos(int centavos) => _peso.format(centavos / 100);

  /// `1234.5` -> `₱1,235`. For coarse figures like a delivery-fee estimate.
  static String formatWhole(num amount) => _pesoWhole.format(amount);

  /// Range display, e.g. `₱120.00 – ₱180.00`, used for stall quote spreads.
  static String range(num low, num high) =>
      '${format(low)} – ${format(high)}';
}
