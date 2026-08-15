import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:palengke_core/palengke_core.dart';

import 'src/app.dart';
import 'src/app_config.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await PalengkeFormatting.ensureInitialized();

  runApp(
    ProviderScope(
      overrides: [palengkeConfigProvider.overrideWithValue(buildDevConfig())],
      child: const PartnerApp(),
    ),
  );
}
