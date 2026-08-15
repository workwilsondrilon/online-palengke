import '../config/palengke_config.dart';

/// Single source of truth for API paths.
///
/// Paths are relative to [PalengkeConfig.baseUrl] and always start with `/`.
abstract final class ApiEndpoints {
  /// Unauthenticated liveness probe. Used by the M0 "Check API health" button.
  static const String health = '/health';

  /// `POST /api/{role}/auth/otp/request`
  ///
  /// Role-scoped, not a single global path: the server has no other way to
  /// know which role a brand-new phone number should register as — there is
  /// no role field in the request body, because the URL already carries it.
  static String otpRequest(PalengkeRole role) =>
      '/api/${role.pathSegment}/auth/otp/request';

  /// `POST /api/{role}/auth/otp/verify`
  static String otpVerify(PalengkeRole role) =>
      '/api/${role.pathSegment}/auth/otp/verify';

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
  ///
  /// The OTP paths are role-scoped functions, not static constants, so they
  /// can't live in this set literal — [isUnauthenticatedPath] checks them by
  /// suffix instead.
  static const Set<String> unauthenticatedPaths = {health, refresh};

  static bool isUnauthenticatedPath(String path) =>
      unauthenticatedPaths.contains(path) ||
      path.endsWith('/auth/otp/request') ||
      path.endsWith('/auth/otp/verify');
}
