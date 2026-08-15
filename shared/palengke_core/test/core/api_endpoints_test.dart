import 'package:flutter_test/flutter_test.dart';
import 'package:palengke_core/src/config/palengke_config.dart';
import 'package:palengke_core/src/core/api_endpoints.dart';

void main() {
  group('ApiEndpoints role-scoped OTP paths', () {
    test('otpRequest is scoped per role, not a single global path', () {
      expect(ApiEndpoints.otpRequest(PalengkeRole.customer), '/api/customer/auth/otp/request');
      expect(ApiEndpoints.otpRequest(PalengkeRole.partner), '/api/partner/auth/otp/request');
      expect(ApiEndpoints.otpRequest(PalengkeRole.rider), '/api/rider/auth/otp/request');
    });

    test('otpVerify is scoped per role', () {
      expect(ApiEndpoints.otpVerify(PalengkeRole.customer), '/api/customer/auth/otp/verify');
      expect(ApiEndpoints.otpVerify(PalengkeRole.partner), '/api/partner/auth/otp/verify');
      expect(ApiEndpoints.otpVerify(PalengkeRole.rider), '/api/rider/auth/otp/verify');
    });

    test('refresh and signOut are global, not role-scoped', () {
      expect(ApiEndpoints.refresh, '/api/auth/refresh');
      expect(ApiEndpoints.signOut, '/api/auth/signout');
    });
  });

  group('ApiEndpoints.isUnauthenticatedPath', () {
    test('matches the global unauthenticated paths', () {
      expect(ApiEndpoints.isUnauthenticatedPath(ApiEndpoints.health), isTrue);
      expect(ApiEndpoints.isUnauthenticatedPath(ApiEndpoints.refresh), isTrue);
    });

    test('matches role-scoped OTP paths for every role by suffix', () {
      for (final role in PalengkeRole.values) {
        expect(ApiEndpoints.isUnauthenticatedPath(ApiEndpoints.otpRequest(role)), isTrue);
        expect(ApiEndpoints.isUnauthenticatedPath(ApiEndpoints.otpVerify(role)), isTrue);
      }
    });

    test('does not match an authenticated path', () {
      expect(ApiEndpoints.isUnauthenticatedPath(ApiEndpoints.uploadPresign(PalengkeRole.customer)), isFalse);
      expect(ApiEndpoints.isUnauthenticatedPath(ApiEndpoints.signOut), isFalse);
    });

    test('does not match an unrelated path that happens to contain "otp"', () {
      // Guards the suffix-match implementation against a false positive on a
      // path that merely mentions "otp" without actually being an OTP endpoint.
      expect(ApiEndpoints.isUnauthenticatedPath('/api/customer/orders/otp-history'), isFalse);
    });
  });
}
