import 'package:flutter_test/flutter_test.dart';
import 'package:palengke_core/src/auth/auth_dtos.dart';

void main() {
  group('PhoneNumberPh.normalize', () {
    // Mirrors the server-side PhilippineMobileNumber test cases exactly -
    // the two must agree, since the server never trusts client-side
    // normalization and re-validates independently.
    const validShapes = <String, String>{
      '0917 123 4567': '+639171234567',
      '+63 917 123 4567': '+639171234567',
      '63 917 123 4567': '+639171234567',
      '9171234567': '+639171234567',
      '09171234567': '+639171234567',
      '+639171234567': '+639171234567',
    };

    for (final entry in validShapes.entries) {
      test('normalizes "${entry.key}"', () {
        expect(PhoneNumberPh.normalize(entry.key), entry.value);
      });
    }

    test('rejects a number that does not start with 9 after the prefix', () {
      expect(PhoneNumberPh.normalize('0817 123 4567'), isNull);
    });

    test('rejects too few digits', () {
      expect(PhoneNumberPh.normalize('0917 123 456'), isNull);
    });

    test('rejects too many digits', () {
      expect(PhoneNumberPh.normalize('0917 123 45678'), isNull);
    });

    test('rejects an empty string', () {
      expect(PhoneNumberPh.normalize(''), isNull);
    });

    test('rejects non-numeric garbage', () {
      expect(PhoneNumberPh.normalize('not a phone number'), isNull);
    });
  });

  group('PhoneNumberPh.isValid', () {
    test('true for a normalizable number', () => expect(PhoneNumberPh.isValid('09171234567'), isTrue));

    test('false for an invalid number', () => expect(PhoneNumberPh.isValid('12345'), isFalse));
  });

  group('PhoneNumberPh.format', () {
    test('renders E.164 back to the local display shape', () {
      expect(PhoneNumberPh.format('+639171234567'), '0917 123 4567');
    });

    test('returns the input unchanged when it is not a recognisable E.164 PH number', () {
      expect(PhoneNumberPh.format('not-e164'), 'not-e164');
    });
  });
}
