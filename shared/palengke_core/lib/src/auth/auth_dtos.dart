import 'auth_tokens.dart';

/// Minimal identity returned alongside the token pair.
///
/// Deliberately thin: role-specific profile data (stall details, rider
/// vehicle, delivery addresses) belongs to each app's own feature layer, not
/// to shared auth.
class PalengkeUser {
  const PalengkeUser({
    required this.id,
    required this.phoneNumber,
    this.displayName,
    this.roles = const [],
  });

  final String id;

  /// E.164, e.g. `+639171234567`.
  final String phoneNumber;

  final String? displayName;

  final List<String> roles;

  static PalengkeUser fromJson(Map<String, dynamic> json) => PalengkeUser(
        id: json['id']?.toString() ?? '',
        phoneNumber: json['phoneNumber']?.toString() ?? '',
        displayName: json['displayName']?.toString(),
        roles: switch (json['roles']) {
          final List<dynamic> r => r.map((e) => e.toString()).toList(),
          _ => const <String>[],
        },
      );

  @override
  String toString() => 'PalengkeUser($id, $phoneNumber)';
}

/// Request body for `POST /api/auth/otp/request`.
class OtpRequest {
  const OtpRequest({required this.phoneNumber});

  /// Must already be normalised to E.164 — use [PhoneNumberPh.normalize].
  final String phoneNumber;

  Map<String, dynamic> toJson() => {'phoneNumber': phoneNumber};
}

/// Response of `POST /api/auth/otp/request`.
class OtpChallenge {
  const OtpChallenge({
    this.challengeId,
    this.codeLength = 6,
    this.expiresIn = const Duration(minutes: 5),
    this.resendAfter = const Duration(seconds: 60),
  });

  /// Server-side handle for this challenge, echoed back on verify when the
  /// backend chooses to issue one.
  final String? challengeId;

  final int codeLength;
  final Duration expiresIn;
  final Duration resendAfter;

  static OtpChallenge fromJson(Map<String, dynamic> json) => OtpChallenge(
        challengeId: json['challengeId']?.toString(),
        codeLength: _int(json['codeLength']) ?? 6,
        expiresIn: Duration(seconds: _int(json['expiresInSeconds']) ?? 300),
        resendAfter: Duration(seconds: _int(json['resendAfterSeconds']) ?? 60),
      );
}

/// Request body for `POST /api/auth/otp/verify`.
class OtpVerification {
  const OtpVerification({
    required this.phoneNumber,
    required this.code,
    this.challengeId,
  });

  final String phoneNumber;
  final String code;
  final String? challengeId;

  Map<String, dynamic> toJson() => {
        'phoneNumber': phoneNumber,
        'code': code,
        if (challengeId != null) 'challengeId': challengeId,
      };
}

/// Response of `POST /api/auth/otp/verify` and `POST /api/auth/refresh`.
class AuthSessionDto {
  const AuthSessionDto({required this.tokens, this.user});

  final AuthTokens tokens;
  final PalengkeUser? user;

  static AuthSessionDto fromJson(Map<String, dynamic> json) {
    final access = json['accessToken']?.toString() ?? '';
    final refresh = json['refreshToken']?.toString() ?? '';
    if (access.isEmpty || refresh.isEmpty) {
      throw const FormatException(
        'Auth response is missing accessToken/refreshToken',
      );
    }

    DateTime? expiresAt;
    final rawExpiresAt = json['accessTokenExpiresAt'] ?? json['expiresAt'];
    if (rawExpiresAt != null) {
      expiresAt = DateTime.tryParse(rawExpiresAt.toString())?.toUtc();
    } else {
      final seconds = _int(json['expiresIn']);
      if (seconds != null) {
        expiresAt = DateTime.now().toUtc().add(Duration(seconds: seconds));
      }
    }

    return AuthSessionDto(
      tokens: AuthTokens(
        accessToken: access,
        refreshToken: refresh,
        accessTokenExpiresAt: expiresAt,
      ),
      user: switch (json['user']) {
        final Map<dynamic, dynamic> u =>
          PalengkeUser.fromJson(u.map((k, v) => MapEntry(k.toString(), v))),
        _ => null,
      },
    );
  }
}

int? _int(Object? value) => switch (value) {
      final int v => v,
      final num v => v.toInt(),
      final String v => int.tryParse(v),
      _ => null,
    };

/// Philippine mobile number handling.
abstract final class PhoneNumberPh {
  static final RegExp _digits = RegExp(r'\D');

  /// Normalises the shapes Filipino users actually type
  /// (`0917 123 4567`, `+63 917 123 4567`, `63 917 123 4567`, `9171234567`)
  /// into E.164 `+639171234567`. Returns null when the input cannot be a PH
  /// mobile number.
  static String? normalize(String input) {
    var digits = input.replaceAll(_digits, '');
    if (digits.isEmpty) return null;

    if (digits.startsWith('63')) {
      digits = digits.substring(2);
    } else if (digits.startsWith('0')) {
      digits = digits.substring(1);
    }

    // PH mobile subscriber numbers are 10 digits and always begin with 9.
    if (digits.length != 10 || !digits.startsWith('9')) return null;
    return '+63$digits';
  }

  static bool isValid(String input) => normalize(input) != null;

  /// `+639171234567` -> `0917 123 4567` for display.
  static String format(String e164) {
    final digits = e164.replaceAll(_digits, '');
    if (digits.length != 12 || !digits.startsWith('63')) return e164;
    final local = digits.substring(2);
    return '0${local.substring(0, 3)} ${local.substring(3, 6)} '
        '${local.substring(6)}';
  }
}
