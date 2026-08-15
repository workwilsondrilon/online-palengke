/// What the asset is for. The server uses this to pick the bucket prefix,
/// retention policy and moderation rules.
enum UploadPurpose {
  /// Partner KYC: DTI/business permit, valid ID, stall photo.
  kycDocument('kyc_document'),

  /// Partner catalogue: marketing photo for an item the stall carries.
  itemPhoto('item_photo'),

  /// Partner or customer profile avatar.
  avatar('avatar'),

  /// Rider: photo taken at pickup from the stall.
  pickupProof('pickup_proof'),

  /// Rider: proof-of-delivery photo at the customer's door.
  deliveryProof('delivery_proof'),

  /// Customer: photo attached to a shopping-list line or a dispute.
  shoppingListAttachment('shopping_list_attachment');

  const UploadPurpose(this.wireValue);

  final String wireValue;
}

/// Request body for `POST /api/{role}/uploads/presign`.
class PresignRequest {
  const PresignRequest({
    required this.purpose,
    required this.contentType,
    required this.sizeBytes,
    this.fileName,
  });

  final UploadPurpose purpose;

  /// MIME type of the bytes that will actually be PUT, e.g. `image/jpeg`.
  /// Must match the `Content-Type` sent to S3 or the signature will not
  /// validate.
  final String contentType;

  /// Size of the *compressed* bytes. The server may sign a content-length
  /// range around this.
  final int sizeBytes;

  final String? fileName;

  Map<String, dynamic> toJson() => {
        'purpose': purpose.wireValue,
        'contentType': contentType,
        'sizeBytes': sizeBytes,
        if (fileName != null) 'fileName': fileName,
      };
}

/// Response of `POST /api/{role}/uploads/presign`.
class PresignResponse {
  const PresignResponse({
    required this.assetId,
    required this.uploadUrl,
    required this.objectKey,
    this.requiredHeaders = const {},
  });

  final String assetId;

  /// Fully-qualified, time-limited S3 URL. Absolute — it must not be resolved
  /// against the API base URL.
  final String uploadUrl;

  final String objectKey;

  /// Any headers the server folded into the signature (for example
  /// `x-amz-server-side-encryption`). They must be echoed on the PUT verbatim.
  final Map<String, String> requiredHeaders;

  static PresignResponse fromJson(Map<String, dynamic> json) {
    final assetId = json['assetId']?.toString() ?? '';
    final uploadUrl = json['uploadUrl']?.toString() ?? '';
    if (assetId.isEmpty || uploadUrl.isEmpty) {
      throw const FormatException(
        'Presign response is missing assetId/uploadUrl',
      );
    }
    return PresignResponse(
      assetId: assetId,
      uploadUrl: uploadUrl,
      objectKey: json['objectKey']?.toString() ?? '',
      requiredHeaders: switch (json['requiredHeaders']) {
        final Map<dynamic, dynamic> h => h.map(
            (k, v) => MapEntry(k.toString(), v.toString()),
          ),
        _ => const <String, String>{},
      },
    );
  }
}

/// Result of a completed upload.
class UploadedAsset {
  const UploadedAsset({
    required this.assetId,
    required this.objectKey,
    required this.sizeBytes,
    required this.contentType,
    this.url,
  });

  final String assetId;
  final String objectKey;
  final int sizeBytes;
  final String contentType;

  /// Public/CDN URL, when the commit response supplies one.
  final String? url;

  static UploadedAsset fromCommit(
    Map<String, dynamic> json, {
    required String assetId,
    required String objectKey,
    required int sizeBytes,
    required String contentType,
  }) {
    return UploadedAsset(
      assetId: json['assetId']?.toString() ?? assetId,
      objectKey: json['objectKey']?.toString() ?? objectKey,
      sizeBytes: sizeBytes,
      contentType: contentType,
      url: json['url']?.toString(),
    );
  }

  @override
  String toString() => 'UploadedAsset($assetId, $sizeBytes bytes)';
}

/// Upload progress, suitable for driving a `LinearProgressIndicator`.
class UploadProgress {
  const UploadProgress({
    required this.stage,
    this.sentBytes = 0,
    this.totalBytes = 0,
  });

  final UploadStage stage;
  final int sentBytes;
  final int totalBytes;

  /// 0.0–1.0, or null when the total is unknown (show an indeterminate bar).
  double? get fraction {
    if (stage == UploadStage.committing) return 1;
    if (totalBytes <= 0) return null;
    return (sentBytes / totalBytes).clamp(0.0, 1.0);
  }
}

enum UploadStage { compressing, requestingUrl, uploading, committing, done }
