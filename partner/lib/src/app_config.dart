import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:palengke_core/palengke_core.dart';

/// Local dev API port.
///
/// TODO: confirm the real port once the API has a fixed dev-launch profile;
/// this is a placeholder used consistently across all three apps.
const int _devApiPort = 5080;

/// Builds this app's [PalengkeConfig] for local development.
///
/// Base URL differs by platform because "localhost" means different things
/// depending on where the code is actually running:
/// - The Android emulator is its own virtual machine, so its `localhost` is
///   the emulator itself, not the host machine running `dotnet run`. Android
///   reserves `10.0.2.2` as a fixed alias back to the host loopback address.
/// - iOS simulators, desktop targets and the web share the host's network
///   stack directly, so plain `localhost` reaches the API.
///
/// A physical device on the same Wi-Fi needs the host machine's real LAN IP
/// instead of either of these — swap this out manually when testing on one.
PalengkeConfig buildDevConfig() {
  final baseUrl = kIsWeb || !Platform.isAndroid
      ? 'http://localhost:$_devApiPort'
      : 'http://10.0.2.2:$_devApiPort';

  return PalengkeConfig(
    baseUrl: baseUrl,
    environment: PalengkeEnvironment.local,
    role: PalengkeRole.partner,
  );
}
