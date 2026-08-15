/// Which deployment the app is pointed at.
enum PalengkeEnvironment { local, staging, production }

/// Which of the three apps is running. Used to build role-scoped API paths
/// such as `/api/{role}/uploads/presign`.
enum PalengkeRole {
  customer('customer'),
  partner('partner'),
  rider('rider');

  const PalengkeRole(this.pathSegment);

  /// The `{role}` segment used in role-scoped API routes.
  final String pathSegment;
}

/// Immutable runtime configuration.
///
/// This is injected at app startup (see `palengkeConfigProvider`) rather than
/// read from a global, so tests and flavors can substitute their own values.
class PalengkeConfig {
  const PalengkeConfig({
    required this.baseUrl,
    required this.environment,
    required this.role,
    this.connectTimeout = const Duration(seconds: 15),
    this.receiveTimeout = const Duration(seconds: 30),
    this.sendTimeout = const Duration(seconds: 60),
  });

  /// Root of the ASP.NET Core API, with no trailing slash, e.g.
  /// `http://10.0.2.2:5080`.
  final String baseUrl;

  final PalengkeEnvironment environment;

  final PalengkeRole role;

  final Duration connectTimeout;
  final Duration receiveTimeout;

  /// Deliberately generous: this timeout also covers presigned S3 uploads of
  /// photos over mobile data.
  final Duration sendTimeout;

  bool get isLocal => environment == PalengkeEnvironment.local;

  PalengkeConfig copyWith({
    String? baseUrl,
    PalengkeEnvironment? environment,
    PalengkeRole? role,
  }) {
    return PalengkeConfig(
      baseUrl: baseUrl ?? this.baseUrl,
      environment: environment ?? this.environment,
      role: role ?? this.role,
      connectTimeout: connectTimeout,
      receiveTimeout: receiveTimeout,
      sendTimeout: sendTimeout,
    );
  }

  @override
  String toString() =>
      'PalengkeConfig(baseUrl: $baseUrl, env: ${environment.name}, '
      'role: ${role.pathSegment})';
}
