import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../auth/session_manager.dart';
import '../config/palengke_config.dart';
import '../core/api_exception.dart';
import '../core/problem_details.dart';
import '../core/result.dart';
import 'auth_interceptor.dart';
import 'logging_interceptor.dart';
import 'request_options_ext.dart';

/// Converts a decoded JSON body into a domain object.
typedef Decoder<T> = T Function(Object? data);

/// The app's HTTP surface.
///
/// Every method returns a [Result]; a raw [DioException] never escapes.
class ApiClient {
  ApiClient._(this._dio, this.config);

  /// Client for role/user endpoints. Attaches the bearer token and performs
  /// silent refresh on 401.
  factory ApiClient.authenticated({
    required PalengkeConfig config,
    required SessionManager session,
    required TokenRefresher refresher,
    Dio? dio,
  }) {
    final client = dio ?? Dio();
    _applyBaseOptions(client, config);
    client.interceptors.add(
      AuthInterceptor(
        session: session,
        refresher: refresher,
        retryClient: client,
      ),
    );
    if (kDebugMode) client.interceptors.add(const LoggingInterceptor());
    return ApiClient._(client, config);
  }

  /// Client for endpoints that must not carry auth: `/health`, the OTP
  /// endpoints, and `/api/auth/refresh` itself.
  factory ApiClient.public({
    required PalengkeConfig config,
    Dio? dio,
  }) {
    final client = dio ?? Dio();
    _applyBaseOptions(client, config);
    if (kDebugMode) client.interceptors.add(const LoggingInterceptor());
    return ApiClient._(client, config);
  }

  final Dio _dio;
  final PalengkeConfig config;

  /// Escape hatch for callers that genuinely need the Dio instance. Prefer
  /// the typed methods below.
  Dio get raw => _dio;

  static void _applyBaseOptions(Dio dio, PalengkeConfig config) {
    dio.options = dio.options.copyWith(
      baseUrl: config.baseUrl,
      connectTimeout: config.connectTimeout,
      receiveTimeout: config.receiveTimeout,
      sendTimeout: config.sendTimeout,
      headers: {
        ...dio.options.headers,
        'Accept': 'application/json',
      },
      // Let Dio throw on non-2xx so all failures funnel through one mapper.
      responseType: ResponseType.json,
    );
  }

  Future<Result<T>> get<T>(
    String path, {
    required Decoder<T> decode,
    Map<String, dynamic>? queryParameters,
    bool skipAuth = false,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      () => _dio.get<dynamic>(
        path,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: Options(extra: requestExtra(skipAuth: skipAuth)),
      ),
      decode,
    );
  }

  Future<Result<T>> post<T>(
    String path, {
    required Decoder<T> decode,
    Object? body,
    Map<String, dynamic>? queryParameters,
    bool skipAuth = false,
    bool redactBody = false,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      () => _dio.post<dynamic>(
        path,
        data: body,
        queryParameters: queryParameters,
        cancelToken: cancelToken,
        options: Options(
          contentType: Headers.jsonContentType,
          extra: requestExtra(skipAuth: skipAuth, redactBody: redactBody),
        ),
      ),
      decode,
    );
  }

  Future<Result<T>> put<T>(
    String path, {
    required Decoder<T> decode,
    Object? body,
    bool skipAuth = false,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      () => _dio.put<dynamic>(
        path,
        data: body,
        cancelToken: cancelToken,
        options: Options(
          contentType: Headers.jsonContentType,
          extra: requestExtra(skipAuth: skipAuth),
        ),
      ),
      decode,
    );
  }

  Future<Result<T>> delete<T>(
    String path, {
    required Decoder<T> decode,
    Object? body,
    bool skipAuth = false,
    CancelToken? cancelToken,
  }) {
    return _send<T>(
      () => _dio.delete<dynamic>(
        path,
        data: body,
        cancelToken: cancelToken,
        options: Options(extra: requestExtra(skipAuth: skipAuth)),
      ),
      decode,
    );
  }

  Future<Result<T>> _send<T>(
    Future<Response<dynamic>> Function() request,
    Decoder<T> decode,
  ) async {
    try {
      final response = await request();
      try {
        return Ok<T>(decode(response.data));
      } on Object catch (error, stack) {
        // A 2xx we could not understand is a contract violation, not a
        // network problem — surface it loudly rather than as "no data".
        return Err<T>(
          ServerException(
            message: 'The server returned an unexpected response.',
            statusCode: response.statusCode,
            cause: error,
            stackTrace: stack,
          ),
        );
      }
    } on DioException catch (error, stack) {
      return Err<T>(toApiException(error, stack));
    }
  }

  /// Maps a [DioException] onto the sealed [ApiException] hierarchy.
  ///
  /// Exposed so other transports (the presigned S3 upload, which bypasses
  /// this client) can produce the same error types.
  static ApiException toApiException(DioException error, [StackTrace? stack]) {
    final st = stack ?? error.stackTrace;

    switch (error.type) {
      case DioExceptionType.connectionTimeout:
        return NetworkException(
          message: 'Could not reach the server. Check your connection.',
          kind: NetworkFailureKind.connectTimeout,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.sendTimeout:
        return NetworkException(
          message: 'The upload timed out. Please try again.',
          kind: NetworkFailureKind.sendTimeout,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.receiveTimeout:
        return NetworkException(
          message: 'The server took too long to respond.',
          kind: NetworkFailureKind.receiveTimeout,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.connectionError:
        return NetworkException(
          message: 'No connection to the server.',
          kind: NetworkFailureKind.connectionError,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.badCertificate:
        return NetworkException(
          message: 'The server certificate could not be verified.',
          kind: NetworkFailureKind.badCertificate,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.cancel:
        return NetworkException(
          message: 'The request was cancelled.',
          kind: NetworkFailureKind.cancelled,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.badResponse:
        return _fromResponse(error, st);
      case DioExceptionType.transformTimeout:
        // Decoding a very large payload blew the transformer budget. That is
        // a server contract problem, not a connectivity one.
        return ServerException(
          message: 'The server sent more data than we could process.',
          statusCode: error.response?.statusCode,
          cause: error,
          stackTrace: st,
        );
      case DioExceptionType.unknown:
        return NetworkException(
          message: 'Something went wrong reaching the server.',
          cause: error,
          stackTrace: st,
        );
    }
  }

  static ApiException _fromResponse(DioException error, StackTrace? stack) {
    final response = error.response;
    final status = response?.statusCode;
    final problem = ProblemDetails.tryParse(_decodeBody(response?.data));
    final message = problem?.displayMessage ?? _fallbackMessage(status);

    if (status == null) {
      return ServerException(
        message: message,
        problem: problem,
        cause: error,
        stackTrace: stack,
      );
    }

    if (status == 401 || status == 403) {
      return UnauthorizedException(
        message: status == 403
            ? (problem?.detail ?? 'You do not have access to this.')
            : (problem?.detail ?? 'Your session has expired. Please sign in.'),
        statusCode: status,
        problem: problem,
        cause: error,
        stackTrace: stack,
      );
    }

    if (status == 404) {
      return NotFoundException(
        message: message,
        problem: problem,
        cause: error,
        stackTrace: stack,
      );
    }

    // 400/409/422 carry field errors; treat any other 4xx that shipped an
    // `errors` object the same way.
    final isValidation = status == 400 ||
        status == 409 ||
        status == 422 ||
        (status < 500 && (problem?.hasFieldErrors ?? false));
    if (isValidation) {
      return ValidationException(
        message: message,
        fieldErrors: problem?.errors ?? const {},
        statusCode: status,
        problem: problem,
        cause: error,
        stackTrace: stack,
      );
    }

    return ServerException(
      message: message,
      statusCode: status,
      problem: problem,
      cause: error,
      stackTrace: stack,
    );
  }

  /// Reverse proxies love returning `text/plain` or HTML. Try to recover a
  /// JSON object anyway; give up quietly if it is not JSON.
  static Object? _decodeBody(Object? data) {
    if (data is Map) return data;
    if (data is String && data.trimLeft().startsWith('{')) {
      try {
        return jsonDecode(data);
      } on FormatException {
        return null;
      }
    }
    return null;
  }

  static String _fallbackMessage(int? status) {
    if (status == null) return 'The request failed.';
    if (status >= 500) return 'The server had a problem. Please try again.';
    if (status == 429) return 'Too many attempts. Please wait a moment.';
    if (status >= 400) return 'The request was rejected by the server.';
    return 'The request failed ($status).';
  }

  void close({bool force = false}) => _dio.close(force: force);
}

/// Common decoders.
abstract final class Decode {
  /// Discards the body. Use for endpoints that return 204.
  static Unit unit(Object? _) => ();

  /// Body must be a JSON object.
  static Map<String, dynamic> map(Object? data) {
    if (data is Map) return data.map((k, v) => MapEntry(k.toString(), v));
    throw FormatException('Expected a JSON object, got ${data.runtimeType}');
  }

  /// Body as-is, whatever it is. Used by the health check, which may return
  /// `"Healthy"`, a JSON report, or nothing at all depending on how the API
  /// team configures the health middleware.
  static Object? any(Object? data) => data;

  /// Body rendered as text for display.
  static String text(Object? data) {
    if (data == null) return '';
    if (data is String) return data;
    try {
      return const JsonEncoder.withIndent('  ').convert(data);
    } on Object {
      return data.toString();
    }
  }

  /// Body must be a JSON array of objects.
  static List<Map<String, dynamic>> mapList(Object? data) {
    if (data is List) return data.map(map).toList();
    throw FormatException('Expected a JSON array, got ${data.runtimeType}');
  }
}
