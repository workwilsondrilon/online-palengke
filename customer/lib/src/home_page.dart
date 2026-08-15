import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:palengke_core/palengke_core.dart';

import 'eligibility/eligibility_page.dart';

/// Placeholder home screen for M0.
///
/// Its only real job is the "Check API health" button — the acceptance check
/// for this milestone is that each app can reach the backend's `/health`
/// endpoint and render either the raw response or a typed error.
class HomePage extends ConsumerStatefulWidget {
  const HomePage({required this.appName, required this.role, super.key});

  final String appName;
  final PalengkeRole role;

  @override
  ConsumerState<HomePage> createState() => _HomePageState();
}

class _HomePageState extends ConsumerState<HomePage> {
  Result<String>? _healthResult;
  bool _checking = false;

  Future<void> _checkHealth() async {
    setState(() => _checking = true);
    final result = await ref.read(healthApiProvider).check();
    if (!mounted) return;
    setState(() {
      _healthResult = result;
      _checking = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final controller = ref.watch(authControllerProvider);
    final user = controller.state.user;

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.appName),
        actions: [
          IconButton(
            onPressed: controller.state.isBusy ? null : controller.signOut,
            icon: const Icon(Icons.logout_rounded),
            tooltip: 'Sign out',
          ),
        ],
      ),
      body: SafeArea(
        child: Padding(
          padding: Spacing.pageInsets,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Card(
                child: Padding(
                  padding: Spacing.cardInsets,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Signed in', style: Theme.of(context).textTheme.titleMedium),
                      Spacing.gapXs,
                      Text('Role: ${widget.role.pathSegment}'),
                      if (user != null) Text('Phone: ${PhoneNumberPh.format(user.phoneNumber)}'),
                    ],
                  ),
                ),
              ),
              Spacing.gapXl,
              PrimaryButton(
                label: 'Check delivery eligibility',
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(builder: (_) => const EligibilityPage()),
                ),
                icon: Icons.local_shipping_outlined,
              ),
              Spacing.gapXl,
              Text('M0 acceptance check', style: Theme.of(context).textTheme.titleMedium),
              Spacing.gapSm,
              Text(
                'Confirms this app can reach the backend.',
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
              Spacing.gapLg,
              PrimaryButton(
                label: 'Check API health',
                isBusy: _checking,
                onPressed: _checkHealth,
                icon: Icons.wifi_tethering_rounded,
              ),
              Spacing.gapLg,
              switch (_healthResult) {
                null => const SizedBox.shrink(),
                Ok(:final value) => _ResultCard(
                    color: PalengkeColors.success,
                    icon: Icons.check_circle_outline_rounded,
                    title: 'Healthy',
                    detail: value,
                  ),
                Err(:final error) => _ResultCard(
                    color: Theme.of(context).colorScheme.error,
                    icon: Icons.error_outline_rounded,
                    title: error.runtimeType.toString(),
                    detail: error.message,
                  ),
              },
            ],
          ),
        ),
      ),
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({
    required this.color,
    required this.icon,
    required this.title,
    required this.detail,
  });

  final Color color;
  final IconData icon;
  final String title;
  final String detail;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: Spacing.cardInsets,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: color),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: Theme.of(context).textTheme.titleMedium),
                  Spacing.gapXs,
                  SelectableText(detail),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
