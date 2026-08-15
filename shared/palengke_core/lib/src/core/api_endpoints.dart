import '../config/palengke_config.dart';

/// Single source of truth for API paths.
///
/// Paths are relative to [PalengkeConfig.baseUrl] and always start with `/`.
abstract final class ApiEndpoints {
  /// Unauthenticated liveness probe. Used by the M0 "Check API health" button.
  static const String health = '/health';

  static const String otpRequest = '/api/auth/otp/request';
  static const String otpVerify = '/api/auth/otp/verify';
  static const String refresh = '/api/auth/refresh';
  static const String signOut = '/api/auth/signout';

  /// `POST /api/{role}/uploads/presign`
  static String uploadPresign(PalengkeRole role) =>
      '/api/${role.pathSegment}/uploads/presign';

  /// `POST /api/{role}/uploads/{assetId}/commit`
  static String uploadCommit(PalengkeRole role, String assetId) =>
      '/api/${role.pathSegment}/uploads/$assetId/commit';

  /// Paths that must never carry an `Authorization` header and must never
  /// trigger the silent-refresh retry loop (refreshing on a failed refresh
  /// would recurse).
  static const Set<String> unauthenticatedPaths = {
    health,
    otpRequest,
    otpVerify,
    refresh,
  };
}
