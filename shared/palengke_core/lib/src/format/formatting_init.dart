import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';

/// Loads the ICU date symbols the formatters need.
///
/// `intl` compiles number symbols for every locale into the binary, but date
/// symbols are loaded on demand: constructing `DateFormat(..., 'en_PH')`
/// before this runs throws `LocaleDataException`. Every app must call this in
/// `main()` before `runApp`.
abstract final class PalengkeFormatting {
  /// The one locale the platform ships in M0.
  static const String locale = 'en_PH';

  static bool _initialized = false;

  static Future<void> ensureInitialized() async {
    if (_initialized) return;
    await initializeDateFormatting(locale);
    Intl.defaultLocale = locale;
    _initialized = true;
  }
}
