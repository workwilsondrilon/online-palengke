import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../config/palengke_config.dart';
import '../core/api_endpoints.dart';
import '../core/api_exception.dart';
import '../core/result.dart';
import '../network/api_client.dart';
import 'image_compressor.dart';
import 'upload_dtos.dart';

/// Three-step presigned upload, shared by all three apps.
///
/// 1. `POST /api/{role}/uploads/presign` (authenticated)
/// 2. `PUT <uploadUrl>` straight to S3
/// 3. `POST /api/{role}/uploads/{assetId}/commit` (authenticated)
///
/// Step 2 goes through [_storageDio], a **bare** Dio with no interceptors.
/// This is not an optimisation: S3 computes the signature over a fixed set of
/// headers, and an unexpected `Authorization: Bearer …` header makes it
/// respond 400 `InvalidArgument` / 403 `SignatureDoesNotMatch`. Never route
/// this PUT through the app's authenticated client.
class UploadService {
  UploadService({
    required ApiClient client,
    required PalengkeConfig config,
    ImageCompressor? compressor,
    Dio? storageDio,
  })  : _client = client, // ignore: prefer_initializing_formals
        _role = config.role,
        _compressor = compressor ?? const ImageCompressor(),
        _storageDio = storageDio ??
            Dio(
              BaseOptions(
                sendTimeout: config.sendTimeout,
                receiveTimeout: config.receiveTimeout,
                connectTimeout: config.connectTimeout,
              ),
            );

  final ApiClient _client;
  final PalengkeRole _role;
  final ImageCompressor _compressor;
  final Dio _storageDio;

  /// Compress, presign, PUT, commit. Returns the finalised asset (its
  /// `assetId` is what the rest of the API expects).
  ///
  /// [onProgress] fires for every stage, so a UI can show one bar across the
  /// whole operation.
  Future<Result<UploadedAsset>> uploadImage({
    required Uint8List bytes,
    required UploadPurpose purpose,
    String sourceContentType = 'image/jpeg',
    String? fileName,
    void Function(UploadProgress progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    onProgress?.call(const UploadProgress(stage: UploadStage.compressing));

    final compressed = await _compressor.compress(
      bytes,
      sourceContentType: sourceContentType,
    );
    return switch (compressed) {
      Err(:final error) => Err<UploadedAsset>(error),
      Ok(value: final image) => await uploadBytes(
          bytes: image.bytes,
          contentType: image.contentType,
          purpose: purpose,
          fileName: fileName,
          onProgress: onProgress,
          cancelToken: cancelToken,
        ),
    };
  }

  /// Presign, PUT, commit for bytes that are already in their final form
  /// (a PDF permit, an already-compressed image).
  Future<Result<UploadedAsset>> uploadBytes({
    required Uint8List bytes,
    required String contentType,
    required UploadPurpose purpose,
    String? fileName,
    void Function(UploadProgress progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    onProgress?.call(const UploadProgress(stage: UploadStage.requestingUrl));

    final presigned = await _presign(
      PresignRequest(
        purpose: purpose,
        contentType: contentType,
        sizeBytes: bytes.length,
        fileName: fileName,
      ),
      cancelToken: cancelToken,
    );
    final PresignResponse presign;
    switch (presigned) {
      case Ok(:final value):
        presign = value;
      case Err(:final error):
        return Err(error);
    }

    final put = await _putToStorage(
      presign: presign,
      bytes: bytes,
      contentType: contentType,
      onProgress: onProgress,
      cancelToken: cancelToken,
    );
    if (put case Err(:final error)) return Err(error);

    onProgress?.call(
      UploadProgress(
        stage: UploadStage.committing,
        sentBytes: bytes.length,
        totalBytes: bytes.length,
      ),
    );

    final committed = await _commit(
      assetId: presign.assetId,
      objectKey: presign.objectKey,
      sizeBytes: bytes.length,
      contentType: contentType,
      cancelToken: cancelToken,
    );

    if (committed is Ok<UploadedAsset>) {
      onProgress?.call(
        UploadProgress(
          stage: UploadStage.done,
          sentBytes: bytes.length,
          totalBytes: bytes.length,
        ),
      );
    }
    return committed;
  }

  Future<Result<PresignResponse>> _presign(
    PresignRequest request, {
    CancelToken? cancelToken,
  }) {
    return _client.post<PresignResponse>(
      ApiEndpoints.uploadPresign(_role),
      body: request.toJson(),
      cancelToken: cancelToken,
      decode: (data) => PresignResponse.fromJson(Decode.map(data)),
    );
  }

  /// Raw PUT to the presigned URL. No app headers, no interceptors.
  Future<Result<Unit>> _putToStorage({
    required PresignResponse presign,
    required Uint8List bytes,
    required String contentType,
    void Function(UploadProgress progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    try {
      await _storageDio.put<void>(
        presign.uploadUrl,
        data: Stream<List<int>>.value(bytes),
        cancelToken: cancelToken,
        onSendProgress: (sent, total) {
          onProgress?.call(
            UploadProgress(
              stage: UploadStage.uploading,
              sentBytes: sent,
              totalBytes: total > 0 ? total : bytes.length,
            ),
          );
        },
        options: Options(
          // Only the headers S3 signed. Dio adds Content-Length from the
          // header below; nothing else is injected because this Dio has an
          // empty interceptor chain and no base headers.
          headers: <String, dynamic>{
            Headers.contentLengthHeader: bytes.length,
            ...presign.requiredHeaders,
          },
          contentType: contentType,
          responseType: ResponseType.plain,
        ),
      );
      return okUnit;
    } on DioException catch (error, stack) {
      return Err(_mapStorageError(error, stack));
    }
  }

  Future<Result<UploadedAsset>> _commit({
    required String assetId,
    required String objectKey,
    required int sizeBytes,
    required String contentType,
    CancelToken? cancelToken,
  }) {
    return _client.post<UploadedAsset>(
      ApiEndpoints.uploadCommit(_role, assetId),
      body: {
        'objectKey': objectKey,
        'sizeBytes': sizeBytes,
        'contentType': contentType,
      },
      cancelToken: cancelToken,
      decode: (data) => UploadedAsset.fromCommit(
        data is Map ? Decode.map(data) : const <String, dynamic>{},
        assetId: assetId,
        objectKey: objectKey,
        sizeBytes: sizeBytes,
        contentType: contentType,
      ),
    );
  }

  /// S3 answers with XML, not ProblemDetails, so the generic mapper would
  /// produce a confusing message. Translate the cases that actually happen.
  ApiException _mapStorageError(DioException error, StackTrace stack) {
    final status = error.response?.statusCode;
    if (status == null) {
      return ApiClient.toApiException(error, stack);
    }
    if (status == 403) {
      return ServerException(
        message: 'The upload link expired. Please try again.',
        statusCode: status,
        cause: error,
        stackTrace: stack,
      );
    }
    return ServerException(
      message: 'The photo could not be uploaded ($status).',
      statusCode: status,
      cause: error,
      stackTrace: stack,
    );
  }
}
