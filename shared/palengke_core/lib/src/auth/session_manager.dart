import 'dart:async';

import 'package:flutter/foundation.dart';

import '../core/result.dart';
import 'auth_tokens.dart';
import 'token_store.dart';

/// Mints a fresh token pair from a refresh token. Supplied by the auth layer
/// so [SessionManager] does not need to know about HTTP.
typedef TokenRefresher = Future<Result<AuthTokens>> Function(
  String refreshToken,
);

/// Why the session changed. Apps listen to this to bounce the user back to
/// the login screen.
enum SessionEvent {
  /// Tokens were adopted after a successful OTP verification.
  signedIn,

  /// Silent refresh produced a new access token.
  refreshed,

  /// The user asked to sign out.
  signedOut,

  /// Refresh failed (or there was nothing to refresh with). Tokens have been
  /// cleared and the user must log in again.
  expired,
}

/// Outcome of [SessionManager.ensureRefreshed].
enum RefreshOutcome {
  /// This call performed a refresh; retry the original request.
  refreshed,

  /// A concurrent request had already refreshed; retry the original request.
  alreadyFresh,

  /// There was no refresh token, or refresh failed. Session is now cleared.
  failed,
}

/// Owns the in-memory copy of the current tokens and serialises refreshes.
///
/// Why in-memory: `flutter_secure_storage` hits the platform keychain, which
/// is far too slow to await on every outbound request. The store remains the
/// source of truth across app launches.
class SessionManager {
  SessionManager({required TokenStore tokenStore}) : _store = tokenStore;

  final TokenStore _store;
  final StreamController<SessionEvent> _events =
      StreamController<SessionEvent>.broadcast();

  AuthTokens? _tokens;

  /// The single in-flight refresh. Concurrent 401s await this rather than
  /// each firing their own refresh call.
  Future<RefreshOutcome>? _inFlightRefresh;

  bool _restored = false;
  bool _disposed = false;

  /// Emits whenever the session changes. Broadcast, so late listeners are
  /// fine, but they will not see events emitted before they subscribed.
  Stream<SessionEvent> get events => _events.stream;

  AuthTokens? get tokens => _tokens;

  String? get accessToken => _tokens?.accessToken;

  bool get isAuthenticated => _tokens != null;

  /// Whether [restore] has completed. The router should wait on this before
  /// deciding between the login screen and the home screen.
  bool get isRestored => _restored;

  /// Load persisted tokens into memory. Safe to call more than once.
  Future<void> restore() async {
    if (_restored) return;
    try {
      _tokens = await _store.read();
    } on Object catch (error, stack) {
      // A corrupt or inaccessible keychain entry must not brick app startup:
      // treat it as "logged out".
      debugPrint('SessionManager.restore failed: $error\n$stack');
      _tokens = null;
    }
    _restored = true;
  }

  /// Adopt tokens after a successful sign-in.
  Future<void> signIn(AuthTokens tokens) async {
    _tokens = tokens;
    _restored = true;
    await _store.write(tokens);
    _emit(SessionEvent.signedIn);
  }

  /// Drop tokens locally. [expired] distinguishes "the user tapped sign out"
  /// from "the refresh token is dead", which apps usually surface differently.
  Future<void> clear({bool expired = false}) async {
    _tokens = null;
    _restored = true;
    await _store.clear();
    _emit(expired ? SessionEvent.expired : SessionEvent.signedOut);
  }

  /// Called by the auth interceptor on a 401.
  ///
  /// [staleAccessToken] is the token the failing request actually sent. If it
  /// no longer matches the current token, another request already refreshed
  /// while this one was in flight, so we skip straight to a retry instead of
  /// burning a second refresh token.
  ///
  /// Concurrent callers share one refresh: the first caller installs
  /// [_inFlightRefresh] and everyone else awaits it.
  Future<RefreshOutcome> ensureRefreshed({
    required String? staleAccessToken,
    required TokenRefresher refresher,
  }) {
    final current = _tokens;
    if (current == null) {
      return Future<RefreshOutcome>.value(RefreshOutcome.failed);
    }
    if (staleAccessToken != null && staleAccessToken != current.accessToken) {
      return Future<RefreshOutcome>.value(RefreshOutcome.alreadyFresh);
    }

    final existing = _inFlightRefresh;
    if (existing != null) return existing;

    final started = _performRefresh(current.refreshToken, refresher);
    _inFlightRefresh = started;
    // Free the slot once the refresh settles. Callers already awaiting the
    // future hold their own reference, so nulling the field is safe.
    return started.whenComplete(() {
      if (identical(_inFlightRefresh, started)) _inFlightRefresh = null;
    });
  }

  /// Never throws: a dead refresh is a [RefreshOutcome.failed], not an error.
  Future<RefreshOutcome> _performRefresh(
    String refreshToken,
    TokenRefresher refresher,
  ) async {
    Result<AuthTokens> result;
    try {
      result = await refresher(refreshToken);
    } on Object catch (error, stack) {
      debugPrint('SessionManager refresh threw: $error\n$stack');
      await clear(expired: true);
      return RefreshOutcome.failed;
    }

    switch (result) {
      case Ok(:final value):
        _tokens = value;
        await _store.write(value);
        _emit(SessionEvent.refreshed);
        return RefreshOutcome.refreshed;
      case Err():
        // The refresh token is dead, or the server is unreachable and we
        // cannot tell the difference. Either way, force a fresh login.
        await clear(expired: true);
        return RefreshOutcome.failed;
    }
  }

  void _emit(SessionEvent event) {
    if (_disposed || _events.isClosed) return;
    _events.add(event);
  }

  void dispose() {
    _disposed = true;
    unawaited(_events.close());
  }
}
