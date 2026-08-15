import '../core/api_endpoints.dart';
import '../core/result.dart';
import 'api_client.dart';

/// `GET /health`.
///
/// ASP.NET Core's health middleware returns `text/plain` ("Healthy") by
/// default and a JSON report when `ResponseWriter` is customised, so the
/// result is rendered as text either way.
class HealthApi {
  const HealthApi(this._client);

  final ApiClient _client;

  Future<Result<String>> check() {
    return _client.get<String>(
      ApiEndpoints.health,
      skipAuth: true,
      decode: Decode.text,
    );
  }
}
