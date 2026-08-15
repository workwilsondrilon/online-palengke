import 'problem_details.dart';

/// Every failure that can leave `ApiClient` is one of these.
///
/// A raw `DioException` must never escape the networking layer — see
/// `ApiClient._toApiException`.
sealed class ApiException implements Exception {
  const ApiException({
    required this.message,
    this.statusCode,
    this.problem,
    this.cause,
    this.stackTrace,
  });

  /// Safe to show to a user.
  final String message;

  /// HTTP status, when the failure came from a response.
  final int? statusCode;

  /// Parsed RFC 7807 body, when the server sent one.
  final ProblemDetails? problem;

  /// The underlying error (usually a `DioException`), for logging only.
  final Object? cause;

  final StackTrace? stackTrace;

  /// Correlation id for support tickets, if the server supplied one.
  String? get traceId => problem?.traceId;

  @override
  String toString() => '$runtimeType($statusCode): $message';
}

/// No usable HTTP response: DNS failure, connection refused, timeout, TLS
/// error, or a cancelled request. Retrying may help.
final class NetworkException extends ApiException {
  const NetworkException({
    required super.message,
    this.kind = NetworkFailureKind.unknown,
    super.cause,
    super.stackTrace,
  });

  final NetworkFailureKind kind;

  bool get isTimeout =>
      kind == NetworkFailureKind.connectTimeout ||
      kind == NetworkFailureKind.sendTimeout ||
      kind == NetworkFailureKind.receiveTimeout;

  bool get isCancelled => kind == NetworkFailureKind.cancelled;
}

enum NetworkFailureKind {
  connectTimeout,
  sendTimeout,
  receiveTimeout,
  connectionError,
  badCertificate,
  cancelled,
  unknown,
}

/// 401/403. If this reaches a call site it means silent refresh was either
/// impossible or itself failed; the session has been cleared.
final class UnauthorizedException extends ApiException {
  const UnauthorizedException({
    required super.message,
    super.statusCode = 401,
    super.problem,
    super.cause,
    super.stackTrace,
  });

  bool get isForbidden => statusCode == 403;
}

/// 400/409/422 with field-level detail.
final class ValidationException extends ApiException {
  const ValidationException({
    required super.message,
    this.fieldErrors = const {},
    super.statusCode = 400,
    super.problem,
    super.cause,
    super.stackTrace,
  });

  /// Keyed by field name, e.g. `{'phoneNumber': ['Must be a PH mobile number']}`.
  final Map<String, List<String>> fieldErrors;

  /// First message for [field], or null. Wire this into `TextField.errorText`.
  String? errorFor(String field) {
    for (final entry in fieldErrors.entries) {
      if (entry.key.toLowerCase() == field.toLowerCase()) {
        return entry.value.isEmpty ? null : entry.value.first;
      }
    }
    return null;
  }
}

/// 404.
final class NotFoundException extends ApiException {
  const NotFoundException({
    required super.message,
    super.statusCode = 404,
    super.problem,
    super.cause,
    super.stackTrace,
  });
}

/// 5xx, an unmapped status, or a response body we could not decode.
final class ServerException extends ApiException {
  const ServerException({
    required super.message,
    super.statusCode,
    super.problem,
    super.cause,
    super.stackTrace,
  });
}
