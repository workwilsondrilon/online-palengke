import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import 'request_options_ext.dart';

/// Debug-only request/response logging.
///
/// Guarded twice: it is only added to the Dio instance when [kDebugMode] is
/// true, and every write is also behind [kDebugMode] so a release build that
/// somehow installs it still prints nothing.
class LoggingInterceptor extends Interceptor {
  const LoggingInterceptor({this.maxBodyChars = 1200});

  final int maxBodyChars;

  static const _sensitiveHeaders = {
    'authorization',
    'cookie',
    'set-cookie',
    'x-api-key',
  };

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    if (kDebugMode) {
      debugPrint('--> ${options.method} ${options.uri}');
      _printHeaders(options.headers);
      if (options.data != null) {
        debugPrint('    body: ${_body(options.data, options.redactBody)}');
      }
    }
    handler.next(options);
  }

  @override
  void onResponse(
    Response<dynamic> response,
    ResponseInterceptorHandler handler,
  ) {
    if (kDebugMode) {
      final options = response.requestOptions;
      debugPrint(
        '<-- ${response.statusCode} ${options.method} ${options.uri}',
      );
      if (response.data != null) {
        debugPrint('    body: ${_body(response.data, options.redactBody)}');
      }
    }
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (kDebugMode) {
      final options = err.requestOptions;
      debugPrint(
        'xxx ${err.response?.statusCode ?? err.type.name} '
        '${options.method} ${options.uri}',
      );
      final data = err.response?.data;
      if (data != null) {
        debugPrint('    body: ${_body(data, options.redactBody)}');
      } else if (err.message != null) {
        debugPrint('    ${err.message}');
      }
    }
    handler.next(err);
  }

  void _printHeaders(Map<String, dynamic> headers) {
    for (final entry in headers.entries) {
      final value = _sensitiveHeaders.contains(entry.key.toLowerCase())
          ? '<redacted>'
          : entry.value;
      debugPrint('    ${entry.key}: $value');
    }
  }

  String _body(Object? data, bool redact) {
    if (redact) return '<redacted>';
    if (data is List<int>) return '<${data.length} bytes>';
    if (data is Stream) return '<stream>';
    String text;
    try {
      text = data is String ? data : const JsonEncoder().convert(data);
    } on Object {
      text = data.toString();
    }
    if (text.length <= maxBodyChars) return text;
    return '${text.substring(0, maxBodyChars)}… (${text.length} chars)';
  }
}
