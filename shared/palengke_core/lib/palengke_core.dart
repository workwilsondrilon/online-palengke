/// Shared foundation for the three Online Palengke apps
/// (`palengke_client`, `palengke_partner`, `palengke_rider`).
///
/// Contains the HTTP client and typed error model, the phone-OTP auth flow,
/// the presigned S3 upload pipeline, the shared theme, and the formatting
/// helpers for PHP currency and Asia/Manila timestamps.
library;

// Config
export 'src/config/palengke_config.dart';

// Core primitives
export 'src/core/api_endpoints.dart';
export 'src/core/api_exception.dart';
export 'src/core/problem_details.dart';
export 'src/core/result.dart';

// Networking
export 'src/network/api_client.dart' show ApiClient, Decode, Decoder;
export 'src/network/health_api.dart';
export 'src/network/request_options_ext.dart' show RequestFlags;

// Auth
export 'src/auth/auth_api.dart';
export 'src/auth/auth_controller.dart';
export 'src/auth/auth_dtos.dart';
export 'src/auth/auth_state.dart';
export 'src/auth/auth_tokens.dart';
export 'src/auth/session_manager.dart'
    show RefreshOutcome, SessionEvent, SessionManager, TokenRefresher;
export 'src/auth/token_store.dart';

// Uploads
export 'src/uploads/image_compressor.dart';
export 'src/uploads/upload_dtos.dart';
export 'src/uploads/upload_service.dart';

// Theme
export 'src/theme/palengke_theme.dart';
export 'src/theme/palengke_tokens.dart';

// Formatting
export 'src/format/formatting_init.dart';
export 'src/format/manila_time.dart';
export 'src/format/php_money.dart';

// Widgets
export 'src/widgets/primary_button.dart';
export 'src/widgets/state_views.dart';

// Riverpod wiring
export 'src/providers/core_providers.dart';
