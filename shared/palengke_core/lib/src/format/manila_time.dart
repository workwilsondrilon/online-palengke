import 'package:intl/intl.dart';

/// Renders UTC instants from the API in Philippine Standard Time.
///
/// Why a fixed offset rather than the `timezone` package: the Philippines is
/// UTC+08:00 year-round and has observed no daylight saving since 1978, so a
/// constant offset is exact, not an approximation. If the platform ever needs
/// to render a *different* zone, swap this for the `timezone` package — do
/// not add ad-hoc offsets elsewhere.
abstract final class ManilaTime {
  /// PHT — Philippine Standard Time.
  static const Duration offset = Duration(hours: 8);

  static const String abbreviation = 'PHT';

  static final DateFormat _time = DateFormat('h:mm a', 'en_PH');
  static final DateFormat _dayMonth = DateFormat('d MMM', 'en_PH');
  static final DateFormat _dateTime = DateFormat('d MMM y, h:mm a', 'en_PH');
  static final DateFormat _weekdayTime = DateFormat('EEE h:mm a', 'en_PH');
  static final DateFormat _isoDate = DateFormat('yyyy-MM-dd', 'en_PH');

  /// Shifts [instant] into Manila wall-clock time.
  ///
  /// The returned [DateTime] is flagged UTC on purpose: its fields are the
  /// Manila wall-clock values, and marking it UTC stops `DateFormat` from
  /// applying the *device's* offset a second time. Never do arithmetic with
  /// the result — format it and discard it.
  static DateTime toManila(DateTime instant) => instant.toUtc().add(offset);

  /// `3:45 PM`
  static String time(DateTime instant) => _time.format(toManila(instant));

  /// `14 Aug`
  static String dayMonth(DateTime instant) =>
      _dayMonth.format(toManila(instant));

  /// `14 Aug 2026, 3:45 PM`
  static String dateTime(DateTime instant) =>
      _dateTime.format(toManila(instant));

  /// `Fri 5:30 AM` — used for the morning delivery-batch windows.
  static String weekdayTime(DateTime instant) =>
      _weekdayTime.format(toManila(instant));

  /// `2026-08-14`
  static String isoDate(DateTime instant) => _isoDate.format(toManila(instant));

  /// `14 Aug 2026, 3:45 PM PHT`
  static String dateTimeWithZone(DateTime instant) =>
      '${dateTime(instant)} $abbreviation';

  /// Coarse relative label for feeds: `just now`, `12m ago`, `3h ago`,
  /// otherwise an absolute date.
  static String relative(DateTime instant, {DateTime? now}) {
    final reference = (now ?? DateTime.now()).toUtc();
    final delta = reference.difference(instant.toUtc());

    if (delta.isNegative) return dateTime(instant);
    if (delta.inMinutes < 1) return 'just now';
    if (delta.inMinutes < 60) return '${delta.inMinutes}m ago';
    if (delta.inHours < 24) return '${delta.inHours}h ago';
    if (delta.inDays < 7) return '${delta.inDays}d ago';
    return dateTime(instant);
  }

  /// Today's date in Manila, as a plain `yyyy-MM-dd` string. Delivery batches
  /// are keyed by Manila calendar day, which is not the device's day for a
  /// user travelling abroad.
  static String todayInManila({DateTime? now}) =>
      isoDate(now ?? DateTime.now());
}
