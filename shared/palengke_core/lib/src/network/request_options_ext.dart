import 'package:dio/dio.dart';

/// Keys we stash in `RequestOptions.extra` to steer the interceptors.
abstract final class RequestFlags {
  /// Do not attach the bearer token, and do not attempt silent refresh.
  static const String skipAuth = 'palengke.skipAuth';

  /// Set by the auth interceptor once a request has been retried after a
  /// refresh, so a second 401 cannot loop forever.
  static const String didRetryAfterRefresh = 'palengke.didRetryAfterRefresh';

  /// Redact the body in debug logs (OTP codes, tokens, raw image bytes).
  static const String redactBody = 'palengke.redactBody';
}

extension RequestFlagsX on RequestOptions {
  bool get skipAuth => extra[RequestFlags.skipAuth] == true;
  bool get didRetryAfterRefresh =>
      extra[RequestFlags.didRetryAfterRefresh] == true;
  bool get redactBody => extra[RequestFlags.redactBody] == true;
}

/// Builds the `extra` map for a request.
Map<String, dynamic> requestExtra({
  bool skipAuth = false,
  bool redactBody = false,
}) =>
    <String, dynamic>{
      if (skipAuth) RequestFlags.skipAuth: true,
      if (redactBody) RequestFlags.redactBody: true,
    };
