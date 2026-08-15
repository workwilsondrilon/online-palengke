import '../config/palengke_config.dart';
import '../core/api_endpoints.dart';
import '../core/result.dart';
import '../network/api_client.dart';
import 'auth_dtos.dart';
import 'auth_tokens.dart';

/// Thin transport wrapper over the four auth endpoints.
///
/// Built on the *public* [ApiClient] so that:
///   * OTP requests are not blocked by a missing/expired bearer token, and
///   * a 401 from `/api/auth/refresh` cannot recurse into another refresh.
class AuthApi {
  const AuthApi(this._client, this._role);

  final ApiClient _client;

  /// Which app this is — determines the role a brand-new phone number
  /// registers as. See [ApiEndpoints.otpRequest].
  final PalengkeRole _role;

  /// `POST /api/{role}/auth/otp/request`
  Future<Result<OtpChallenge>> requestOtp(OtpRequest request) {
    return _client.post<OtpChallenge>(
      ApiEndpoints.otpRequest(_role),
      body: request.toJson(),
      skipAuth: true,
      decode: (data) => OtpChallenge.fromJson(Decode.map(data)),
    );
  }

  /// `POST /api/{role}/auth/otp/verify`
  Future<Result<AuthSessionDto>> verifyOtp(OtpVerification verification) {
    return _client.post<AuthSessionDto>(
      ApiEndpoints.otpVerify(_role),
      body: verification.toJson(),
      skipAuth: true,
      // The OTP code is a credential; keep it out of the debug log.
      redactBody: true,
      decode: (data) => AuthSessionDto.fromJson(Decode.map(data)),
    );
  }

  /// `POST /api/auth/refresh`
  Future<Result<AuthTokens>> refresh(String refreshToken) async {
    final result = await _client.post<AuthSessionDto>(
      ApiEndpoints.refresh,
      body: {'refreshToken': refreshToken},
      skipAuth: true,
      redactBody: true,
      decode: (data) => AuthSessionDto.fromJson(Decode.map(data)),
    );
    return result.map((session) => session.tokens);
  }

  /// `POST /api/auth/signout`
  ///
  /// Sent with the refresh token so the server can revoke it. Failure here is
  /// not fatal — the caller clears local state regardless.
  Future<Result<Unit>> signOut(String refreshToken) {
    return _client.post<Unit>(
      ApiEndpoints.signOut,
      body: {'refreshToken': refreshToken},
      skipAuth: true,
      redactBody: true,
      decode: Decode.unit,
    );
  }
}
