import '../core/api_exception.dart';
import 'auth_dtos.dart';

enum AuthStatus {
  /// Persisted tokens have not been read yet. Show a splash, not the login
  /// screen — otherwise a signed-in user sees a flash of login on every cold
  /// start.
  unknown,

  /// No session. Show the phone-number step.
  unauthenticated,

  /// An OTP has been requested. Show the code step.
  awaitingCode,

  authenticated,
}

/// Everything the login flow and the app shell need to render.
class AuthState {
  const AuthState({
    this.status = AuthStatus.unknown,
    this.phoneNumber,
    this.challenge,
    this.user,
    this.isBusy = false,
    this.error,
    this.sessionExpired = false,
  });

  final AuthStatus status;

  /// E.164 number the OTP was sent to.
  final String? phoneNumber;

  final OtpChallenge? challenge;

  final PalengkeUser? user;

  /// A request is in flight; disable the submit button.
  final bool isBusy;

  /// Last failure, already mapped to the typed error model.
  final ApiException? error;

  /// Set when the user was kicked out by a failed silent refresh rather than
  /// by tapping sign out, so the UI can explain why.
  final bool sessionExpired;

  bool get isAuthenticated => status == AuthStatus.authenticated;

  /// Field error for the phone input, if the last failure was a validation
  /// error naming it.
  String? get phoneFieldError => _fieldError(const ['phoneNumber', 'phone']);

  /// Field error for the OTP input.
  String? get codeFieldError => _fieldError(const ['code', 'otp', 'otpCode']);

  String? _fieldError(List<String> names) {
    final e = error;
    if (e is! ValidationException) return null;
    for (final name in names) {
      final message = e.errorFor(name);
      if (message != null) return message;
    }
    return null;
  }

  AuthState copyWith({
    AuthStatus? status,
    String? phoneNumber,
    OtpChallenge? challenge,
    PalengkeUser? user,
    bool? isBusy,
    ApiException? error,
    bool clearError = false,
    bool? sessionExpired,
  }) {
    return AuthState(
      status: status ?? this.status,
      phoneNumber: phoneNumber ?? this.phoneNumber,
      challenge: challenge ?? this.challenge,
      user: user ?? this.user,
      isBusy: isBusy ?? this.isBusy,
      error: clearError ? null : (error ?? this.error),
      sessionExpired: sessionExpired ?? this.sessionExpired,
    );
  }

  @override
  String toString() =>
      'AuthState(${status.name}, busy: $isBusy, error: ${error?.message})';
}
