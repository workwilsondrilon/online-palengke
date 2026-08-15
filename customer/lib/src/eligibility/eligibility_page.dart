import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:palengke_core/palengke_core.dart';

import 'eligibility_api.dart';
import 'eligibility_dtos.dart';

/// Lets a customer check whether Online Palengke delivers to a given point.
///
/// A full interactive map-pin picker (flutter_map) is the eventual plan; this form -
/// direct latitude/longitude entry - is the deliberately simpler v1 the plan explicitly
/// allows for, so the eligibility check has a real screen without a new map dependency
/// blocking it.
class EligibilityPage extends ConsumerStatefulWidget {
  const EligibilityPage({super.key});

  @override
  ConsumerState<EligibilityPage> createState() => _EligibilityPageState();
}

class _EligibilityPageState extends ConsumerState<EligibilityPage> {
  // Manila, so the fields never open blank - real customers won't know their own
  // coordinates offhand, but this gives anyone testing the screen a known-good starting
  // point to edit from rather than a wall of empty inputs.
  final _latController = TextEditingController(text: '14.5995');
  final _lngController = TextEditingController(text: '120.9842');

  bool _checking = false;
  Result<EligibilityResult>? _result;
  String? _latError;
  String? _lngError;

  @override
  void dispose() {
    _latController.dispose();
    _lngController.dispose();
    super.dispose();
  }

  Future<void> _check() async {
    final lat = double.tryParse(_latController.text.trim());
    final lng = double.tryParse(_lngController.text.trim());

    setState(() {
      _latError =
          (lat == null || lat < -90 || lat > 90) ? 'Enter a latitude between -90 and 90' : null;
      _lngError =
          (lng == null || lng < -180 || lng > 180) ? 'Enter a longitude between -180 and 180' : null;
    });

    if (lat == null || lng == null || _latError != null || _lngError != null) {
      return;
    }

    setState(() => _checking = true);
    final result = await ref.read(eligibilityApiProvider).check(lat: lat, lng: lng);
    if (!mounted) return;
    setState(() {
      _result = result;
      _checking = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Delivery check')),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: Spacing.pageInsets,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Will we deliver to you?', style: Theme.of(context).textTheme.titleLarge),
              Spacing.gapSm,
              Text(
                'Enter your delivery address coordinates. Find them by long-pressing your '
                'location in any map app and copying the pin.',
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
              Spacing.gapXl,
              TextField(
                controller: _latController,
                keyboardType: const TextInputType.numberWithOptions(decimal: true, signed: true),
                enabled: !_checking,
                decoration: InputDecoration(
                  labelText: 'Latitude',
                  prefixIcon: const Icon(Icons.explore_outlined),
                  errorText: _latError,
                ),
              ),
              Spacing.gapMd,
              TextField(
                controller: _lngController,
                keyboardType: const TextInputType.numberWithOptions(decimal: true, signed: true),
                enabled: !_checking,
                decoration: InputDecoration(
                  labelText: 'Longitude',
                  prefixIcon: const Icon(Icons.explore_outlined),
                  errorText: _lngError,
                ),
                onSubmitted: (_) => _check(),
              ),
              Spacing.gapXl,
              PrimaryButton(
                label: 'Check this address',
                isBusy: _checking,
                onPressed: _check,
                icon: Icons.local_shipping_outlined,
              ),
              Spacing.gapXl,
              switch (_result) {
                null => const SizedBox.shrink(),
                Ok(:final value) => _EligibilityResultCard(result: value),
                Err(:final error) => ErrorBanner(error: error),
              },
            ],
          ),
        ),
      ),
    );
  }
}

class _EligibilityResultCard extends StatelessWidget {
  const _EligibilityResultCard({required this.result});

  final EligibilityResult result;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final color = result.isEligible ? PalengkeColors.success : scheme.error;

    return Card(
      child: Padding(
        padding: Spacing.cardInsets,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  result.isEligible
                      ? Icons.check_circle_outline_rounded
                      : Icons.location_off_outlined,
                  color: color,
                ),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: Text(
                    result.isEligible ? 'We deliver here' : 'Outside our delivery area',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
              ],
            ),
            Spacing.gapSm,
            Text(result.message),
            if (result.markets.isNotEmpty) ...[
              Spacing.gapLg,
              for (final market in result.markets)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
                  child: Row(
                    children: [
                      const Icon(Icons.storefront_outlined, size: 20),
                      const SizedBox(width: Spacing.sm),
                      Expanded(
                        child: Text('${market.name} — ${market.city}, ${market.province}'),
                      ),
                    ],
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}
