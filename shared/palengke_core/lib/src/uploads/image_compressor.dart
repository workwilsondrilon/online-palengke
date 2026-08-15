import 'dart:ui' as ui;

import 'package:flutter/foundation.dart';
import 'package:flutter_image_compress/flutter_image_compress.dart';

import '../core/api_exception.dart';
import '../core/result.dart';

/// Bytes ready to be PUT to S3.
class CompressedImage {
  const CompressedImage({
    required this.bytes,
    required this.contentType,
    required this.width,
    required this.height,
    required this.wasCompressed,
  });

  final Uint8List bytes;
  final String contentType;
  final int width;
  final int height;

  /// False when the source was already small enough, or when the platform has
  /// no compression backend (see [ImageCompressor.isSupportedOnThisPlatform]).
  final bool wasCompressed;

  int get sizeBytes => bytes.length;
}

/// Shrinks camera images before they touch the network.
///
/// Phone cameras in this market routinely produce 4–8 MB JPEGs; uploading
/// those over mobile data is the single biggest source of failed uploads, so
/// compression happens before presigning (the presign call declares the final
/// byte count).
class ImageCompressor {
  const ImageCompressor({
    this.maxSizeBytes = defaultMaxSizeBytes,
    this.maxDimension = defaultMaxDimension,
  });

  /// Hard cap: 2 MB.
  static const int defaultMaxSizeBytes = 2 * 1024 * 1024;

  /// Longest edge, in pixels.
  static const int defaultMaxDimension = 1600;

  /// Quality ladder. Each step is tried in turn until the result fits.
  static const List<int> _qualitySteps = [88, 78, 68, 58, 45, 35];

  final int maxSizeBytes;
  final int maxDimension;

  /// `flutter_image_compress` ships backends for Android, iOS, macOS, web and
  /// OpenHarmony — but not Windows or Linux. On those platforms compression
  /// is skipped (see [compress]); this is only reachable in desktop dev
  /// builds, never on a shipped mobile app.
  static bool get isSupportedOnThisPlatform {
    if (kIsWeb) return true;
    return switch (defaultTargetPlatform) {
      TargetPlatform.android ||
      TargetPlatform.iOS ||
      TargetPlatform.macOS =>
        true,
      TargetPlatform.windows ||
      TargetPlatform.linux ||
      TargetPlatform.fuchsia =>
        false,
    };
  }

  /// Compresses [source] to fit [maxSizeBytes] and [maxDimension].
  ///
  /// Always re-encodes as JPEG when compression runs, so the caller must use
  /// the returned [CompressedImage.contentType] for both the presign call and
  /// the S3 PUT.
  Future<Result<CompressedImage>> compress(
    Uint8List source, {
    String sourceContentType = 'image/jpeg',
  }) async {
    if (source.isEmpty) {
      return const Err(
        ValidationException(message: 'The selected file is empty.'),
      );
    }

    final dimensions = await _readDimensions(source);
    if (dimensions == null) {
      return const Err(
        ValidationException(
          message: 'That file does not look like an image we can read.',
        ),
      );
    }
    final (srcWidth, srcHeight) = dimensions;

    final withinSize = source.length <= maxSizeBytes;
    final withinDimensions =
        srcWidth <= maxDimension && srcHeight <= maxDimension;
    if (withinSize && withinDimensions) {
      return Ok(
        CompressedImage(
          bytes: source,
          contentType: sourceContentType,
          width: srcWidth,
          height: srcHeight,
          wasCompressed: false,
        ),
      );
    }

    if (!isSupportedOnThisPlatform) {
      // Deliberate, documented gap rather than a silent oversized upload.
      if (withinSize) {
        return Ok(
          CompressedImage(
            bytes: source,
            contentType: sourceContentType,
            width: srcWidth,
            height: srcHeight,
            wasCompressed: false,
          ),
        );
      }
      return Err(
        ValidationException(
          message: 'Images must be under '
              '${(maxSizeBytes / (1024 * 1024)).toStringAsFixed(0)} MB on this '
              'platform. Pick a smaller image.',
        ),
      );
    }

    var (targetWidth, targetHeight) = _scaleToFit(
      srcWidth,
      srcHeight,
      maxDimension,
    );

    try {
      // Two passes: walk the quality ladder at the target size, then, if the
      // image is still too big, halve the dimensions and walk it again.
      for (var attempt = 0; attempt < 3; attempt++) {
        for (final quality in _qualitySteps) {
          final bytes = await FlutterImageCompress.compressWithList(
            source,
            minWidth: targetWidth,
            minHeight: targetHeight,
            quality: quality,
            format: CompressFormat.jpeg,
            keepExif: false,
          );
          if (bytes.length <= maxSizeBytes) {
            return Ok(
              CompressedImage(
                bytes: bytes,
                contentType: 'image/jpeg',
                width: targetWidth,
                height: targetHeight,
                wasCompressed: true,
              ),
            );
          }
        }
        targetWidth = (targetWidth / 2).round().clamp(64, maxDimension);
        targetHeight = (targetHeight / 2).round().clamp(64, maxDimension);
      }
    } on Object catch (error, stack) {
      return Err(
        ValidationException(
          message: 'We could not process that image.',
          cause: error,
          stackTrace: stack,
        ),
      );
    }

    return const Err(
      ValidationException(
        message: 'That image is too large even after compression. '
            'Please choose another photo.',
      ),
    );
  }

  /// Reads the header only — no full decode, so this stays cheap on a
  /// low-end handset.
  Future<(int, int)?> _readDimensions(Uint8List bytes) async {
    ui.ImmutableBuffer? buffer;
    ui.ImageDescriptor? descriptor;
    try {
      buffer = await ui.ImmutableBuffer.fromUint8List(bytes);
      descriptor = await ui.ImageDescriptor.encoded(buffer);
      return (descriptor.width, descriptor.height);
    } on Object {
      return null;
    } finally {
      descriptor?.dispose();
      buffer?.dispose();
    }
  }

  static (int, int) _scaleToFit(int width, int height, int maxEdge) {
    final longest = width > height ? width : height;
    if (longest <= maxEdge) return (width, height);
    final scale = maxEdge / longest;
    return (
      (width * scale).round().clamp(1, maxEdge),
      (height * scale).round().clamp(1, maxEdge),
    );
  }
}
