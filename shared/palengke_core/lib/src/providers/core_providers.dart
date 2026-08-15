import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_api.dart';
import '../auth/auth_controller.dart';
import '../auth/auth_state.dart';
import '../auth/session_manager.dart';
import '../auth/token_store.dart';
import '../config/palengke_config.dart';
import '../network/api_client.dart';
import '../network/health_api.dart';
import '../uploads/image_compressor.dart';
import '../uploads/upload_service.dart';

/// Runtime configuration. **Every app must override this** in its root
/// `ProviderScope`:
///
/// ```dart
/// ProviderScope(
///   overrides: [palengkeConfigProvider.overrideWithValue(myConfig)],
///   child: const MyApp(),
/// )
/// ```
///
/// It throws rather than defaulting, so a missing override fails loudly at
/// startup instead of silently pointing at the wrong environment.
final palengkeConfigProvider = Provider<PalengkeConfig>(
  (ref) => throw UnimplementedError(
    'palengkeConfigProvider must be overridden in the root ProviderScope.',
  ),
);

/// Secure storage for tokens. Override with `InMemoryTokenStore` in tests.
final tokenStoreProvider = Provider<TokenStore>(
  (ref) => SecureTokenStore(),
);

/// In-memory session state plus the single-flight refresh coordinator.
final sessionManagerProvider = Provider<SessionManager>((ref) {
  final manager = SessionManager(tokenStore: ref.watch(tokenStoreProvider));
  ref.onDispose(manager.dispose);
  return manager;
});

/// HTTP client for endpoints that must not carry a bearer token: `/health`,
/// the OTP endpoints and `/api/auth/refresh`. Keeping these on a separate
/// client is what stops a failing refresh from recursing into itself.
final publicApiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient.public(config: ref.watch(palengkeConfigProvider));
  ref.onDispose(client.close);
  return client;
});

final authApiProvider = Provider<AuthApi>(
  (ref) => AuthApi(ref.watch(publicApiClientProvider)),
);

/// HTTP client for everything else: attaches the bearer token and performs
/// silent refresh on 401.
final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient.authenticated(
    config: ref.watch(palengkeConfigProvider),
    session: ref.watch(sessionManagerProvider),
    // Read (not watch) the auth API lazily at refresh time; watching here
    // would be a dependency cycle.
    refresher: (refreshToken) => ref.read(authApiProvider).refresh(refreshToken),
  );
  ref.onDispose(client.close);
  return client;
});

final healthApiProvider = Provider<HealthApi>(
  (ref) => HealthApi(ref.watch(publicApiClientProvider)),
);

final imageCompressorProvider = Provider<ImageCompressor>(
  (ref) => const ImageCompressor(),
);

final uploadServiceProvider = Provider<UploadService>(
  (ref) => UploadService(
    client: ref.watch(apiClientProvider),
    config: ref.watch(palengkeConfigProvider),
    compressor: ref.watch(imageCompressorProvider),
  ),
);

/// The phone-OTP flow controller.
final authControllerProvider = ChangeNotifierProvider<AuthController>((ref) {
  return AuthController(
    api: ref.watch(authApiProvider),
    session: ref.watch(sessionManagerProvider),
  );
});

/// Convenience: watch just the state, so widgets rebuild on state changes
/// rather than on controller identity.
final authStateProvider = Provider<AuthState>(
  (ref) => ref.watch(authControllerProvider).state,
);
