import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:palengke_core/palengke_core.dart';

import 'home_page.dart';
import 'login_page.dart';

const _appName = 'Palengke Rider';

class RiderApp extends StatelessWidget {
  const RiderApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: _appName,
      debugShowCheckedModeBanner: false,
      theme: PalengkeTheme.light(),
      darkTheme: PalengkeTheme.dark(),
      home: const _AuthGate(),
    );
  }
}

/// Reads any persisted session before the first frame that matters, then
/// routes between [LoginPage] and [HomePage] off [AuthStatus].
class _AuthGate extends ConsumerStatefulWidget {
  const _AuthGate();

  @override
  ConsumerState<_AuthGate> createState() => _AuthGateState();
}

class _AuthGateState extends ConsumerState<_AuthGate> {
  @override
  void initState() {
    super.initState();
    // Fire-and-forget: bootstrap() only ever sets state on this same
    // provider, so the rebuild it triggers is all the signalling this needs.
    unawaited(ref.read(authControllerProvider).bootstrap());
  }

  @override
  Widget build(BuildContext context) {
    final status = ref.watch(authControllerProvider).state.status;

    return switch (status) {
      AuthStatus.unknown => const Scaffold(body: LoadingView()),
      AuthStatus.unauthenticated ||
      AuthStatus.awaitingCode =>
        const LoginPage(appName: _appName),
      AuthStatus.authenticated =>
        const HomePage(appName: _appName, role: PalengkeRole.rider),
    };
  }
}
