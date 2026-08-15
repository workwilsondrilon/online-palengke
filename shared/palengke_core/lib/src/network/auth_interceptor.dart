import 'package:dio/dio.dart';

import '../auth/session_manager.dart';
import '../core/api_endpoints.dart';
import 'request_options_ext.dart';

/// Attaches the bearer token and performs silent refresh on 401.
///
/// Concurrency: N simultaneous requests that all 401 will all call
/// [SessionManager.ensureRefreshed], which collapses them onto one in-flight
/// refresh. Only one refresh call ever hits the network.
class AuthInterceptor extends Interceptor {
  AuthInterceptor({
    required SessionManager session,
    required TokenRefresher refresher,
    required Dio retryClient,
  })  : _session = session, // ignore: prefer_initializing_formals
        _refresher = refresher, // ignore: prefer_initializing_formals
        _retryClient = retryClient; // ignore: prefer_initializing_formals

  final SessionManager _session;
  final TokenRefresher _refresher;

  /// Used to replay the original request after a refresh. This is the same
  /// Dio the request came from; replaying through it is fine because the
  /// retried request carries [RequestFlags.didRetryAfterRefresh].
  final Dio _retryClient;

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) {
    if (!options.skipAuth) {
      final token = _session.accessToken;
      if (token != null) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (!_shouldAttemptRefresh(err)) {
      handler.next(err);
      return;
    }

    final options = err.requestOptions;
    final staleHeader = options.headers['Authorization']?.toString();
    final staleToken = staleHeader != null && staleHeader.startsWith('Bearer ')
        ? staleHeader.substring('Bearer '.length)
        : null;

    final outcome = await _session.ensureRefreshed(
      staleAccessToken: staleToken,
      refresher: _refresher,
    );

    if (outcome == RefreshOutcome.failed) {
      // SessionManager has already cleared tokens and emitted
      // SessionEvent.expired. Let the 401 surface as UnauthorizedException.
      handler.next(err);
      return;
    }

    try {
      final response = await _replay(options);
      handler.resolve(response);
    } on DioException catch (retryError) {
      handler.next(retryError);
    } on Object catch (retryError) {
      handler.next(
        DioException(
          requestOptions: options,
          error: retryError,
          type: DioExceptionType.unknown,
        ),
      );
    }
  }

  Future<Response<dynamic>> _replay(RequestOptions options) {
    final token = _session.accessToken;
    return _retryClient.request<dynamic>(
      options.path,
      data: options.data,
      queryParameters: options.queryParameters,
      cancelToken: options.cancelToken,
      onSendProgress: options.onSendProgress,
      onReceiveProgress: options.onReceiveProgress,
      options: Options(
        method: options.method,
        headers: {
          ...options.headers,
          if (token != null) 'Authorization': 'Bearer $token',
        },
        responseType: options.responseType,
        contentType: options.contentType,
        sendTimeout: options.sendTimeout,
        receiveTimeout: options.receiveTimeout,
        followRedirects: options.followRedirects,
        validateStatus: options.validateStatus,
        extra: {
          ...options.extra,
          RequestFlags.didRetryAfterRefresh: true,
        },
      ),
    );
  }

  bool _shouldAttemptRefresh(DioException err) {
    if (err.response?.statusCode != 401) return false;
    final options = err.requestOptions;
    if (options.skipAuth) return false;
    if (options.didRetryAfterRefresh) return false;
    // Never refresh in reaction to the refresh call itself.
    if (ApiEndpoints.isUnauthenticatedPath(options.path)) return false;
    if (!_session.isAuthenticated) return false;
    return true;
  }
}
