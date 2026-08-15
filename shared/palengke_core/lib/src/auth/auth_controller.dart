import 'dart:async';

import 'package:flutter/foundation.dart';

import '../core/api_exception.dart';
import '../core/result.dart';
import 'auth_api.dart';
import 'auth_dtos.dart';
import 'auth_state.dart';
import 'session_manager.dart';

/// Drives the phone-OTP flow and keeps [AuthState] in sync with the session.
///
/// Plain [ChangeNotifier]-free design: it is a [ValueListenable] so it can be
/// exposed through Riverpod (see `authControllerProvider`) without the shared
/// package taking a hard dependency on any particular provider generation.
class AuthController extends ValueNotifier<AuthState> {
  AuthController({
    required this._api,
    required this._session,
  }) : super(const AuthState()) {
    _sessionSub = _session.events.listen(_onSessionEvent);
  }

  final AuthApi _api;
  final SessionManager _session;
  late final StreamSubscription<SessionEvent> _sessionSub;

  bool _disposed = false;

  AuthState get state => value;

  /// Read persisted tokens and decide the initial screen. Call once from the
  /// app shell before rendering the first route.
  Future<void> bootstrap() async {
    await _session.restore();
    _set(
      value.copyWith(
        status: _session.isAuthenticated
            ? AuthStatus.authenticated
            : AuthStatus.unauthenticated,
        clearError: true,
      ),
    );
  }

  /// Step 1: send an OTP to [rawPhoneNumber] (any format the user typed).
  ///
  /// Returns true when the code step should be shown.
  Future<bool> requestOtp(String rawPhoneNumber) async {
    final e164 = PhoneNumberPh.normalize(rawPhoneNumber);
    if (e164 == null) {
      _set(
        value.copyWith(
          isBusy: false,
          error: const ValidationException(
            message: 'Enter a valid Philippine mobile number.',
            fieldErrors: {
              'phoneNumber': ['Enter a valid Philippine mobile number.'],
            },
          ),
        ),
      );
      return false;
    }

    _set(value.copyWith(isBusy: true, clearError: true));

    final result = await _api.requestOtp(OtpRequest(phoneNumber: e164));
    if (_disposed) return false;

    switch (result) {
      case Ok(value: final otpChallenge):
        _set(
          value.copyWith(
            status: AuthStatus.awaitingCode,
            phoneNumber: e164,
            challenge: otpChallenge,
            isBusy: false,
            clearError: true,
            sessionExpired: false,
          ),
        );
        return true;
      case Err(:final error):
        _set(value.copyWith(isBusy: false, error: error));
        return false;
    }
  }

  /// Step 2: exchange the code for a token pair.
  Future<bool> verifyOtp(String code) async {
    final phone = value.phoneNumber;
    if (phone == null) {
      _set(
        value.copyWith(
          status: AuthStatus.unauthenticated,
          error: const ValidationException(
            message: 'Start again — we lost track of your number.',
          ),
        ),
      );
      return false;
    }

    final trimmed = code.trim();
    if (trimmed.isEmpty) {
      _set(
        value.copyWith(
          error: const ValidationException(
            message: 'Enter the code we sent you.',
            fieldErrors: {
              'code': ['Enter the code we sent you.'],
            },
          ),
        ),
      );
      return false;
    }

    _set(value.copyWith(isBusy: true, clearError: true));

    final result = await _api.verifyOtp(
      OtpVerification(
        phoneNumber: phone,
        code: trimmed,
        challengeId: value.challenge?.challengeId,
      ),
    );
    if (_disposed) return false;

    switch (result) {
      case Ok(value: final session):
        await _session.signIn(session.tokens);
        if (_disposed) return false;
        _set(
          value.copyWith(
            status: AuthStatus.authenticated,
            user: session.user,
            isBusy: false,
            clearError: true,
            sessionExpired: false,
          ),
        );
        return true;
      case Err(:final error):
        _set(value.copyWith(isBusy: false, error: error));
        return false;
    }
  }

  /// Go back to the phone-number step (wrong number, or resend flow).
  void restartFlow() {
    _set(
      const AuthState(status: AuthStatus.unauthenticated),
    );
  }

  /// Re-send a code to the number already captured.
  Future<bool> resendOtp() async {
    final phone = value.phoneNumber;
    if (phone == null) return false;
    return requestOtp(phone);
  }

  /// Revoke server-side then clear locally. Local state is cleared even when
  /// the server call fails — a user who taps sign out must end up signed out.
  Future<void> signOut() async {
    _set(value.copyWith(isBusy: true, clearError: true));
    final refreshToken = _session.tokens?.refreshToken;
    if (refreshToken != null) {
      await _api.signOut(refreshToken);
    }
    await _session.clear();
    if (_disposed) return;
    _set(
      const AuthState(status: AuthStatus.unauthenticated),
    );
  }

  void clearError() {
    if (value.error != null) _set(value.copyWith(clearError: true));
  }

  void _onSessionEvent(SessionEvent event) {
    switch (event) {
      case SessionEvent.expired:
        _set(
          const AuthState(
            status: AuthStatus.unauthenticated,
            sessionExpired: true,
          ),
        );
      case SessionEvent.signedOut:
        if (value.status != AuthStatus.unauthenticated) {
          _set(const AuthState(status: AuthStatus.unauthenticated));
        }
      case SessionEvent.signedIn:
      case SessionEvent.refreshed:
        break;
    }
  }

  void _set(AuthState next) {
    if (_disposed) return;
    value = next;
  }

  @override
  void dispose() {
    _disposed = true;
    unawaited(_sessionSub.cancel());
    super.dispose();
  }
}
