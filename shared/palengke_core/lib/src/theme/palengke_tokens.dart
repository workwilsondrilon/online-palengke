import 'package:flutter/widgets.dart';

/// The palette. Widgets must never hardcode a `Color`; read from
/// `Theme.of(context).colorScheme` or from `PalengkeColors` here.
///
/// Direction: fresh-produce greens as the primary voice, warm earth tones
/// (kalabasa orange, banig brown) as the accent — the colours of a Philippine
/// wet market at 5am. Saturation is kept moderate so photographs of produce,
/// which are themselves highly saturated, stay the loudest thing on screen.
abstract final class PalengkeColors {
  // Primary — malunggay / pechay green.
  static const Color green900 = Color(0xFF10361D);
  static const Color green700 = Color(0xFF1B5E31);
  static const Color green600 = Color(0xFF227540);
  static const Color green500 = Color(0xFF2E8B4F);
  static const Color green200 = Color(0xFFA7D8B4);
  static const Color green100 = Color(0xFFD6EDDD);
  static const Color green50 = Color(0xFFEDF7F0);

  // Secondary — kalabasa (squash) orange.
  static const Color clay700 = Color(0xFF8C3F16);
  static const Color clay500 = Color(0xFFC4622D);
  static const Color clay200 = Color(0xFFF2C9AE);
  static const Color clay50 = Color(0xFFFCEFE6);

  // Tertiary — saffron / ginger, used for badges and highlights.
  static const Color saffron600 = Color(0xFFB8801C);
  static const Color saffron400 = Color(0xFFE8A33D);
  static const Color saffron100 = Color(0xFFFBEBCB);

  // Neutrals — warm, banig-inspired rather than blue-grey.
  static const Color ink900 = Color(0xFF1A1C18);
  static const Color ink700 = Color(0xFF3D4038);
  static const Color ink500 = Color(0xFF6B6F64);
  static const Color ink300 = Color(0xFFC3C7BB);
  static const Color ink100 = Color(0xFFE6E5DD);
  static const Color paper = Color(0xFFFBF9F3);
  static const Color white = Color(0xFFFFFFFF);

  // Dark-mode surfaces.
  static const Color darkBase = Color(0xFF12140F);
  static const Color darkSurface = Color(0xFF1B1E18);
  static const Color darkSurfaceHigh = Color(0xFF262A22);

  // Status.
  static const Color danger = Color(0xFFB3261E);
  static const Color dangerLight = Color(0xFFFFDAD5);
  static const Color warning = Color(0xFFB26A00);
  static const Color success = Color(0xFF2E7D32);
  static const Color info = Color(0xFF1F6C93);
}

/// 4pt spacing scale. Use these instead of literal `EdgeInsets` values.
abstract final class Spacing {
  static const double xxs = 2;
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 24;
  static const double xxl = 32;
  static const double xxxl = 48;

  static const EdgeInsets pageInsets = EdgeInsets.symmetric(
    horizontal: lg,
    vertical: xl,
  );
  static const EdgeInsets cardInsets = EdgeInsets.all(lg);
  static const SizedBox gapXs = SizedBox(height: xs, width: xs);
  static const SizedBox gapSm = SizedBox(height: sm, width: sm);
  static const SizedBox gapMd = SizedBox(height: md, width: md);
  static const SizedBox gapLg = SizedBox(height: lg, width: lg);
  static const SizedBox gapXl = SizedBox(height: xl, width: xl);
}

/// Corner radii.
abstract final class Radii {
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double pill = 999;

  static const BorderRadius smAll = BorderRadius.all(Radius.circular(sm));
  static const BorderRadius mdAll = BorderRadius.all(Radius.circular(md));
  static const BorderRadius lgAll = BorderRadius.all(Radius.circular(lg));
  static const BorderRadius pillAll = BorderRadius.all(Radius.circular(pill));
}

/// Minimum tap target. Riders and vendors use these apps one-handed, often
/// wet, often in a hurry — 48dp is a floor, not a target.
abstract final class Sizes {
  static const double minTapTarget = 48;
  static const double buttonHeight = 52;
  static const double inputHeight = 56;
}
