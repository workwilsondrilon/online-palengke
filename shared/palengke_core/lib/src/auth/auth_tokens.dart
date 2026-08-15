/// A JWT access token plus the refresh token that can mint a new one.
class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.refreshToken,
    this.accessTokenExpiresAt,
  });

  final String accessToken;
  final String refreshToken;

  /// UTC instant at which [accessToken] stops being accepted, when the server
  /// tells us. Purely advisory — the 401 refresh path is the real safety net.
  final DateTime? accessTokenExpiresAt;

  bool get isEmpty => accessToken.isEmpty || refreshToken.isEmpty;

  @override
  String toString() => 'AuthTokens(access: <redacted>, refresh: <redacted>)';
}
