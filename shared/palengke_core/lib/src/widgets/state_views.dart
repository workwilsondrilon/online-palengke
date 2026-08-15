import 'package:flutter/material.dart';

import '../core/api_exception.dart';
import '../theme/palengke_tokens.dart';
import 'primary_button.dart';

/// Centred spinner with an optional caption.
class LoadingView extends StatelessWidget {
  const LoadingView({super.key, this.message});

  final String? message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const CircularProgressIndicator(),
          if (message != null) ...[
            const SizedBox(height: Spacing.lg),
            Text(message!, style: Theme.of(context).textTheme.bodyMedium),
          ],
        ],
      ),
    );
  }
}

/// Renders an [ApiException] with an icon and message chosen from its type,
/// plus a retry affordance where retrying makes sense.
class ErrorView extends StatelessWidget {
  const ErrorView({
    required this.error,
    super.key,
    this.onRetry,
    this.retryLabel = 'Try again',
  });

  final ApiException error;
  final VoidCallback? onRetry;
  final String retryLabel;

  /// Retrying a 404 or a validation failure just repeats the failure.
  bool get _isRetryable => switch (error) {
        NetworkException() || ServerException() => true,
        UnauthorizedException() ||
        ValidationException() ||
        NotFoundException() =>
          false,
      };

  IconData get _icon => switch (error) {
        NetworkException() => Icons.wifi_off_rounded,
        UnauthorizedException() => Icons.lock_outline_rounded,
        ValidationException() => Icons.error_outline_rounded,
        NotFoundException() => Icons.search_off_rounded,
        ServerException() => Icons.cloud_off_rounded,
      };

  String get _title => switch (error) {
        NetworkException() => 'No connection',
        UnauthorizedException() => 'Please sign in again',
        ValidationException() => 'Check the details',
        NotFoundException() => 'Not found',
        ServerException() => 'Something went wrong',
      };

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Center(
      child: Padding(
        padding: Spacing.pageInsets,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Icon(_icon, size: 44, color: scheme.error),
            const SizedBox(height: Spacing.lg),
            Text(
              _title,
              style: theme.textTheme.titleMedium,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Spacing.sm),
            Text(
              error.message,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: scheme.onSurfaceVariant),
              textAlign: TextAlign.center,
            ),
            if (error is ValidationException) ...[
              const SizedBox(height: Spacing.sm),
              _FieldErrorList(error: error as ValidationException),
            ],
            if (error.traceId != null) ...[
              const SizedBox(height: Spacing.sm),
              SelectableText(
                'Ref: ${error.traceId}',
                style: theme.textTheme.bodySmall,
              ),
            ],
            if (onRetry != null && _isRetryable) ...[
              const SizedBox(height: Spacing.xl),
              PrimaryButton(
                label: retryLabel,
                onPressed: onRetry,
                expand: false,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _FieldErrorList extends StatelessWidget {
  const _FieldErrorList({required this.error});

  final ValidationException error;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.bodySmall;
    return Column(
      children: [
        for (final entry in error.fieldErrors.entries)
          for (final message in entry.value)
            Text('• $message', style: style, textAlign: TextAlign.center),
      ],
    );
  }
}

/// "Nothing here yet" state.
class EmptyView extends StatelessWidget {
  const EmptyView({
    required this.title,
    super.key,
    this.message,
    this.icon = Icons.inbox_outlined,
    this.action,
  });

  final String title;
  final String? message;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: Spacing.pageInsets,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 44, color: theme.colorScheme.onSurfaceVariant),
            const SizedBox(height: Spacing.lg),
            Text(
              title,
              style: theme.textTheme.titleMedium,
              textAlign: TextAlign.center,
            ),
            if (message != null) ...[
              const SizedBox(height: Spacing.sm),
              Text(
                message!,
                style: theme.textTheme.bodyMedium
                    ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                textAlign: TextAlign.center,
              ),
            ],
            if (action != null) ...[
              const SizedBox(height: Spacing.xl),
              action!,
            ],
          ],
        ),
      ),
    );
  }
}

/// One place to decide between loading / error / empty / content.
///
/// ```dart
/// StateScaffold<List<Quote>>(
///   isLoading: state.isLoading,
///   error: state.error,
///   data: state.quotes,
///   isEmpty: (q) => q.isEmpty,
///   onRetry: controller.reload,
///   emptyTitle: 'No quotes yet',
///   builder: (context, quotes) => QuoteList(quotes),
/// )
/// ```
class StateScaffold<T> extends StatelessWidget {
  const StateScaffold({
    required this.builder,
    super.key,
    this.isLoading = false,
    this.error,
    this.data,
    this.isEmpty,
    this.onRetry,
    this.loadingMessage,
    this.emptyTitle = 'Nothing here yet',
    this.emptyMessage,
    this.emptyIcon = Icons.inbox_outlined,
  });

  final bool isLoading;
  final ApiException? error;
  final T? data;

  /// Called when [data] is present, to decide whether to show [EmptyView].
  final bool Function(T data)? isEmpty;

  final VoidCallback? onRetry;
  final String? loadingMessage;
  final String emptyTitle;
  final String? emptyMessage;
  final IconData emptyIcon;
  final Widget Function(BuildContext context, T data) builder;

  @override
  Widget build(BuildContext context) {
    // Loading wins only when there is nothing to show yet; a refresh over
    // existing data should not blank the screen.
    if (isLoading && data == null) {
      return LoadingView(message: loadingMessage);
    }
    final err = error;
    if (err != null && data == null) {
      return ErrorView(error: err, onRetry: onRetry);
    }
    final value = data;
    if (value == null) {
      return LoadingView(message: loadingMessage);
    }
    if (isEmpty?.call(value) ?? false) {
      return EmptyView(
        title: emptyTitle,
        message: emptyMessage,
        icon: emptyIcon,
      );
    }
    return builder(context, value);
  }
}

/// Inline error banner for form screens, where a full-page [ErrorView] would
/// throw away what the user already typed.
class ErrorBanner extends StatelessWidget {
  const ErrorBanner({required this.error, super.key, this.onDismiss});

  final ApiException? error;
  final VoidCallback? onDismiss;

  @override
  Widget build(BuildContext context) {
    final err = error;
    if (err == null) return const SizedBox.shrink();

    final scheme = Theme.of(context).colorScheme;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: Spacing.lg,
        vertical: Spacing.md,
      ),
      decoration: BoxDecoration(
        color: scheme.errorContainer,
        borderRadius: Radii.mdAll,
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            Icons.error_outline_rounded,
            size: 20,
            color: scheme.onErrorContainer,
          ),
          const SizedBox(width: Spacing.md),
          Expanded(
            child: Text(
              err.message,
              style: Theme.of(context)
                  .textTheme
                  .bodyMedium
                  ?.copyWith(color: scheme.onErrorContainer),
            ),
          ),
          if (onDismiss != null)
            IconButton(
              onPressed: onDismiss,
              icon: const Icon(Icons.close_rounded, size: 18),
              color: scheme.onErrorContainer,
              visualDensity: VisualDensity.compact,
              tooltip: 'Dismiss',
            ),
        ],
      ),
    );
  }
}
