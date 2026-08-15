import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:palengke_core/palengke_core.dart';

import 'eligibility_dtos.dart';

/// `POST /api/customer/markets/eligibility` - given a candidate delivery point, which
/// markets (if any) will actually deliver there.
class EligibilityApi {
  const EligibilityApi(this._client);

  final ApiClient _client;

  Future<Result<EligibilityResult>> check({
    required double lat,
    required double lng,
  }) {
    return _client.post<EligibilityResult>(
      '/api/customer/markets/eligibility',
      body: {'lat': lat, 'lng': lng},
      decode: (data) => EligibilityResult.fromJson(Decode.map(data)),
    );
  }
}

final eligibilityApiProvider = Provider<EligibilityApi>(
  (ref) => EligibilityApi(ref.watch(apiClientProvider)),
);
