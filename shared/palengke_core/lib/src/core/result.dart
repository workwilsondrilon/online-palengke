import 'api_exception.dart';

/// The one failure idiom used across the whole codebase.
///
/// Everything that can fail returns `Future<Result<T>>`; nothing throws
/// [ApiException] across a layer boundary. Call sites are expected to
/// `switch` on the result:
///
/// ```dart
/// switch (await api.health()) {
///   case Ok(:final value):  showHealth(value);
///   case Err(:final error): showError(error.message);
/// }
/// ```
sealed class Result<T> {
  const Result();

  /// Runs [body] and converts a thrown [ApiException] into an [Err].
  /// Any other throwable is a genuine bug and is deliberately left to
  /// propagate.
  static Future<Result<T>> guard<T>(Future<T> Function() body) async {
    try {
      return Ok<T>(await body());
    } on ApiException catch (e) {
      return Err<T>(e);
    }
  }

  bool get isOk => this is Ok<T>;
  bool get isErr => this is Err<T>;

  T? get valueOrNull => switch (this) {
        Ok<T>(:final value) => value,
        Err<T>() => null,
      };

  ApiException? get errorOrNull => switch (this) {
        Ok<T>() => null,
        Err<T>(:final error) => error,
      };

  /// Collapse both branches into a single value.
  R fold<R>({
    required R Function(T value) ok,
    required R Function(ApiException error) err,
  }) =>
      switch (this) {
        Ok<T>(:final value) => ok(value),
        Err<T>(:final error) => err(error),
      };

  /// Transform the success value, preserving any error.
  Result<R> map<R>(R Function(T value) transform) => switch (this) {
        Ok<T>(:final value) => Ok<R>(transform(value)),
        Err<T>(:final error) => Err<R>(error),
      };

  /// Chain another fallible step.
  Future<Result<R>> flatMap<R>(
    Future<Result<R>> Function(T value) next,
  ) async =>
      switch (this) {
        Ok<T>(:final value) => await next(value),
        Err<T>(:final error) => Err<R>(error),
      };
}

final class Ok<T> extends Result<T> {
  const Ok(this.value);

  final T value;

  @override
  String toString() => 'Ok($value)';
}

final class Err<T> extends Result<T> {
  const Err(this.error);

  final ApiException error;

  @override
  String toString() => 'Err($error)';
}

/// Result of an operation that yields no value.
typedef Unit = ();

/// Convenience for `Ok<Unit>(())`.
const Result<Unit> okUnit = Ok<Unit>(());
