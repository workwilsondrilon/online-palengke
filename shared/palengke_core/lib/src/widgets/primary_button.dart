import 'package:flutter/material.dart';

import '../theme/palengke_tokens.dart';

/// The one call-to-action button. Full width by default, 52dp tall, with a
/// built-in busy state so screens do not each invent their own spinner.
class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    required this.label,
    required this.onPressed,
    super.key,
    this.isBusy = false,
    this.icon,
    this.expand = true,
  });

  final String label;

  /// Null disables the button. While [isBusy] the callback is suppressed
  /// regardless, so a double-tap cannot fire two requests.
  final VoidCallback? onPressed;

  final bool isBusy;
  final IconData? icon;

  /// False renders an intrinsic-width button (for use inside a Row).
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final enabled = onPressed != null && !isBusy;

    final child = isBusy
        ? SizedBox.square(
            dimension: 20,
            child: CircularProgressIndicator(
              strokeWidth: 2.5,
              color: scheme.onPrimary,
            ),
          )
        : Row(
            mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (icon != null) ...[
                Icon(icon, size: 20),
                const SizedBox(width: Spacing.sm),
              ],
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.center,
                ),
              ),
            ],
          );

    final button = FilledButton(
      onPressed: enabled ? onPressed : null,
      style: expand
          ? null
          : FilledButton.styleFrom(
              minimumSize: const Size(0, Sizes.buttonHeight),
              padding: const EdgeInsets.symmetric(horizontal: Spacing.xl),
            ),
      child: child,
    );

    return expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}

/// Lower-emphasis companion to [PrimaryButton].
class SecondaryButton extends StatelessWidget {
  const SecondaryButton({
    required this.label,
    required this.onPressed,
    super.key,
    this.icon,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: OutlinedButton(
        onPressed: onPressed,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 20),
              const SizedBox(width: Spacing.sm),
            ],
            Flexible(child: Text(label, overflow: TextOverflow.ellipsis)),
          ],
        ),
      ),
    );
  }
}
