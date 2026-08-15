/// RFC 7807 `application/problem+json` payload as emitted by ASP.NET Core.
///
/// ASP.NET Core's `ValidationProblemDetails` adds an `errors` object of
/// `{ "FieldName": ["message", ...] }`, which we surface as [errors].
class ProblemDetails {
  const ProblemDetails({
    this.type,
    this.title,
    this.status,
    this.detail,
    this.instance,
    this.traceId,
    this.errors = const {},
    this.extensions = const {},
  });

  final String? type;
  final String? title;
  final int? status;
  final String? detail;
  final String? instance;

  /// ASP.NET Core stamps a `traceId` extension; invaluable in bug reports.
  final String? traceId;

  /// Field-level validation errors, keyed by field name.
  final Map<String, List<String>> errors;

  /// Any non-standard members that were present on the payload.
  final Map<String, Object?> extensions;

  bool get hasFieldErrors => errors.isNotEmpty;

  /// Best available human-readable text.
  String get displayMessage {
    final d = detail?.trim();
    if (d != null && d.isNotEmpty) return d;
    final t = title?.trim();
    if (t != null && t.isNotEmpty) return t;
    if (hasFieldErrors) return flattenedFieldErrors;
    return 'The request could not be completed.';
  }

  String get flattenedFieldErrors =>
      errors.entries.map((e) => e.value.join(' ')).join('\n');

  static const Set<String> _knownMembers = {
    'type',
    'title',
    'status',
    'detail',
    'instance',
    'errors',
    'traceId',
  };

  /// Tolerant parser: the backend is not built yet, and proxies/load balancers
  /// happily return HTML or plain text on failure. Returns `null` when the
  /// payload is not a JSON object.
  static ProblemDetails? tryParse(Object? body) {
    if (body is! Map) return null;
    final map = body.map((k, v) => MapEntry(k.toString(), v));

    final rawErrors = map['errors'];
    final errors = <String, List<String>>{};
    if (rawErrors is Map) {
      rawErrors.forEach((key, value) {
        final field = key.toString();
        if (value is List) {
          errors[field] = value.map((e) => e.toString()).toList();
        } else if (value != null) {
          errors[field] = [value.toString()];
        }
      });
    }

    final extensions = <String, Object?>{
      for (final entry in map.entries)
        if (!_knownMembers.contains(entry.key)) entry.key: entry.value,
    };

    return ProblemDetails(
      type: map['type']?.toString(),
      title: map['title']?.toString(),
      status: switch (map['status']) {
        final int s => s,
        final String s => int.tryParse(s),
        _ => null,
      },
      detail: map['detail']?.toString(),
      instance: map['instance']?.toString(),
      traceId: map['traceId']?.toString(),
      errors: errors,
      extensions: extensions,
    );
  }

  @override
  String toString() =>
      'ProblemDetails(status: $status, title: $title, detail: $detail, '
      'errors: $errors)';
}
