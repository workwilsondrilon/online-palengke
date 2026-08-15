import 'package:flutter/material.dart';

import 'palengke_tokens.dart';

/// The shared Material 3 theme for all three apps.
///
/// Apps must not define their own `ThemeData`; if one needs a variation, add
/// it here so the three stay recognisably one product family.
abstract final class PalengkeTheme {
  static ThemeData light() => _build(_lightScheme, Brightness.light);

  static ThemeData dark() => _build(_darkScheme, Brightness.dark);

  static const ColorScheme _lightScheme = ColorScheme(
    brightness: Brightness.light,
    primary: PalengkeColors.green700,
    onPrimary: PalengkeColors.white,
    primaryContainer: PalengkeColors.green100,
    onPrimaryContainer: PalengkeColors.green900,
    secondary: PalengkeColors.clay500,
    onSecondary: PalengkeColors.white,
    secondaryContainer: PalengkeColors.clay50,
    onSecondaryContainer: PalengkeColors.clay700,
    tertiary: PalengkeColors.saffron600,
    onTertiary: PalengkeColors.white,
    tertiaryContainer: PalengkeColors.saffron100,
    onTertiaryContainer: PalengkeColors.ink900,
    error: PalengkeColors.danger,
    onError: PalengkeColors.white,
    errorContainer: PalengkeColors.dangerLight,
    onErrorContainer: Color(0xFF410E0B),
    surface: PalengkeColors.paper,
    onSurface: PalengkeColors.ink900,
    surfaceContainerLowest: PalengkeColors.white,
    surfaceContainerLow: Color(0xFFF7F4EC),
    surfaceContainer: Color(0xFFF1EEE5),
    surfaceContainerHigh: Color(0xFFEBE8DF),
    surfaceContainerHighest: Color(0xFFE5E2D8),
    onSurfaceVariant: PalengkeColors.ink500,
    outline: PalengkeColors.ink300,
    outlineVariant: PalengkeColors.ink100,
    inverseSurface: PalengkeColors.ink900,
    onInverseSurface: PalengkeColors.paper,
    inversePrimary: PalengkeColors.green200,
    shadow: Color(0xFF000000),
    scrim: Color(0xFF000000),
  );

  static const ColorScheme _darkScheme = ColorScheme(
    brightness: Brightness.dark,
    primary: PalengkeColors.green200,
    onPrimary: PalengkeColors.green900,
    primaryContainer: PalengkeColors.green700,
    onPrimaryContainer: PalengkeColors.green100,
    secondary: PalengkeColors.clay200,
    onSecondary: Color(0xFF4A1D05),
    secondaryContainer: PalengkeColors.clay700,
    onSecondaryContainer: PalengkeColors.clay50,
    tertiary: PalengkeColors.saffron400,
    onTertiary: Color(0xFF3F2A00),
    tertiaryContainer: Color(0xFF6B4A00),
    onTertiaryContainer: PalengkeColors.saffron100,
    error: Color(0xFFFFB4AB),
    onError: Color(0xFF690005),
    errorContainer: Color(0xFF93000A),
    onErrorContainer: PalengkeColors.dangerLight,
    surface: PalengkeColors.darkBase,
    onSurface: Color(0xFFE3E3DB),
    surfaceContainerLowest: Color(0xFF0D0F0A),
    surfaceContainerLow: PalengkeColors.darkSurface,
    surfaceContainer: Color(0xFF1F221C),
    surfaceContainerHigh: PalengkeColors.darkSurfaceHigh,
    surfaceContainerHighest: Color(0xFF31352C),
    onSurfaceVariant: Color(0xFFC3C8BC),
    outline: Color(0xFF8D9287),
    outlineVariant: Color(0xFF434840),
    inverseSurface: Color(0xFFE3E3DB),
    onInverseSurface: PalengkeColors.ink900,
    inversePrimary: PalengkeColors.green700,
    shadow: Color(0xFF000000),
    scrim: Color(0xFF000000),
  );

  static ThemeData _build(ColorScheme scheme, Brightness brightness) {
    final base = ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      brightness: brightness,
      scaffoldBackgroundColor: scheme.surface,
      visualDensity: VisualDensity.standard,
    );

    final text = _textTheme(base.textTheme, scheme);

    return base.copyWith(
      textTheme: text,
      appBarTheme: AppBarTheme(
        backgroundColor: scheme.surface,
        foregroundColor: scheme.onSurface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 2,
        centerTitle: false,
        titleTextStyle: text.titleLarge,
      ),
      cardTheme: CardThemeData(
        color: scheme.surfaceContainerLowest,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: Radii.lgAll,
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(Sizes.buttonHeight),
          shape: const RoundedRectangleBorder(borderRadius: Radii.mdAll),
          textStyle: text.labelLarge,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(Sizes.buttonHeight),
          shape: const RoundedRectangleBorder(borderRadius: Radii.mdAll),
          side: BorderSide(color: scheme.outline),
          textStyle: text.labelLarge,
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(0, Sizes.minTapTarget),
          textStyle: text.labelLarge,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerLow,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: Spacing.lg,
          vertical: Spacing.lg,
        ),
        border: OutlineInputBorder(
          borderRadius: Radii.mdAll,
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: Radii.mdAll,
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: Radii.mdAll,
          borderSide: BorderSide(color: scheme.primary, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: Radii.mdAll,
          borderSide: BorderSide(color: scheme.error),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: Radii.mdAll,
          borderSide: BorderSide(color: scheme.error, width: 2),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: scheme.inverseSurface,
        contentTextStyle: text.bodyMedium?.copyWith(
          color: scheme.onInverseSurface,
        ),
        shape: const RoundedRectangleBorder(borderRadius: Radii.mdAll),
      ),
      dividerTheme: DividerThemeData(
        color: scheme.outlineVariant,
        space: 1,
        thickness: 1,
      ),
      chipTheme: ChipThemeData(
        shape: const RoundedRectangleBorder(borderRadius: Radii.pillAll),
        side: BorderSide(color: scheme.outlineVariant),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: scheme.primary,
        linearTrackColor: scheme.surfaceContainerHigh,
      ),
    );
  }

  /// Typography tokens. Roboto (the platform default) is kept deliberately —
  /// bundling a custom font is a decision for the brand milestone, not M0.
  static TextTheme _textTheme(TextTheme base, ColorScheme scheme) {
    // Colourise first, then tune weights, so the per-style colour override on
    // `bodySmall` below is not clobbered by the blanket body colour.
    final t = base.apply(
      bodyColor: scheme.onSurface,
      displayColor: scheme.onSurface,
    );

    return t.copyWith(
      displaySmall: t.displaySmall?.copyWith(
        fontWeight: FontWeight.w700,
        letterSpacing: -0.5,
      ),
      headlineMedium: t.headlineMedium?.copyWith(
        fontWeight: FontWeight.w700,
        letterSpacing: -0.4,
      ),
      headlineSmall: t.headlineSmall?.copyWith(
        fontWeight: FontWeight.w700,
        letterSpacing: -0.3,
      ),
      titleLarge: t.titleLarge?.copyWith(fontWeight: FontWeight.w600),
      titleMedium: t.titleMedium?.copyWith(fontWeight: FontWeight.w600),
      bodyLarge: t.bodyLarge?.copyWith(height: 1.45),
      bodyMedium: t.bodyMedium?.copyWith(height: 1.45),
      bodySmall: t.bodySmall?.copyWith(
        height: 1.4,
        color: scheme.onSurfaceVariant,
      ),
      labelLarge: t.labelLarge?.copyWith(
        fontWeight: FontWeight.w600,
        letterSpacing: 0.1,
      ),
    );
  }
}
