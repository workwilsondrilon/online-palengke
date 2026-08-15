import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'auth_tokens.dart';

/// Persistence for auth tokens, backed by the platform keychain / keystore.
abstract interface class TokenStore {
  Future<AuthTokens?> read();
  Future<void> write(AuthTokens tokens);
  Future<void> clear();
}

/// `flutter_secure_storage` implementation.
///
/// Android (plugin v11) always encrypts with AES-GCM behind a KeyStore-wrapped
/// key; the old `encryptedSharedPreferences` flag no longer exists. Biometric
/// gating is deliberately off — a rider must be able to open the app with wet
/// hands at 4am.
///
/// On iOS/macOS the Keychain item uses `first_unlock` accessibility so a token
/// refresh triggered from the background still works after a reboot, before
/// the user has opened the app.
class SecureTokenStore implements TokenStore {
  SecureTokenStore({FlutterSecureStorage? storage})
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(
                preferencesKeyPrefix: 'palengke',
                resetOnError: true,
              ),
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock,
              ),
              mOptions: MacOsOptions(
                accessibility: KeychainAccessibility.first_unlock,
              ),
            );

  final FlutterSecureStorage _storage;

  static const _kAccess = 'palengke.access_token';
  static const _kRefresh = 'palengke.refresh_token';
  static const _kExpiry = 'palengke.access_expires_at';

  @override
  Future<AuthTokens?> read() async {
    final access = await _storage.read(key: _kAccess);
    final refresh = await _storage.read(key: _kRefresh);
    if (access == null || refresh == null) return null;
    if (access.isEmpty || refresh.isEmpty) return null;

    final rawExpiry = await _storage.read(key: _kExpiry);
    return AuthTokens(
      accessToken: access,
      refreshToken: refresh,
      accessTokenExpiresAt:
          rawExpiry == null ? null : DateTime.tryParse(rawExpiry)?.toUtc(),
    );
  }

  @override
  Future<void> write(AuthTokens tokens) async {
    await _storage.write(key: _kAccess, value: tokens.accessToken);
    await _storage.write(key: _kRefresh, value: tokens.refreshToken);
    final expiry = tokens.accessTokenExpiresAt;
    if (expiry == null) {
      await _storage.delete(key: _kExpiry);
    } else {
      await _storage.write(
        key: _kExpiry,
        value: expiry.toUtc().toIso8601String(),
      );
    }
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _kAccess);
    await _storage.delete(key: _kRefresh);
    await _storage.delete(key: _kExpiry);
  }
}

/// In-memory store for tests and for desktop dev where the platform keychain
/// may not be available.
class InMemoryTokenStore implements TokenStore {
  AuthTokens? _tokens;

  @override
  Future<AuthTokens?> read() async => _tokens;

  @override
  Future<void> write(AuthTokens tokens) async => _tokens = tokens;

  @override
  Future<void> clear() async => _tokens = null;
}
