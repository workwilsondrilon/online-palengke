import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:palengke_core/palengke_core.dart';

/// Phone-OTP sign-in: enter a PH mobile number, then the code sent to it.
///
/// Renders purely off [AuthState] — it does not track its own "which step"
/// flag, because [AuthController] already encodes that in [AuthStatus].
class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({required this.appName, super.key});

  final String appName;

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _phoneController = TextEditingController();
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _phoneController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = ref.watch(authControllerProvider);
    final state = controller.state;

    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: Spacing.pageInsets,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: Spacing.xxxl),
              Text(widget.appName, style: Theme.of(context).textTheme.headlineMedium),
              Spacing.gapSm,
              Text(
                state.status == AuthStatus.awaitingCode
                    ? 'Enter the code we sent to ${PhoneNumberPh.format(state.phoneNumber ?? '')}'
                    : 'Sign in with your mobile number',
                style: Theme.of(context)
                    .textTheme
                    .bodyMedium
                    ?.copyWith(color: Theme.of(context).colorScheme.onSurfaceVariant),
              ),
              Spacing.gapXl,
              if (state.error != null) ...[
                ErrorBanner(error: state.error),
                Spacing.gapLg,
              ],
              if (state.status == AuthStatus.awaitingCode)
                _CodeStep(controller: controller, codeController: _codeController)
              else
                _PhoneStep(controller: controller, phoneController: _phoneController),
            ],
          ),
        ),
      ),
    );
  }
}

class _PhoneStep extends StatelessWidget {
  const _PhoneStep({required this.controller, required this.phoneController});

  final AuthController controller;
  final TextEditingController phoneController;

  @override
  Widget build(BuildContext context) {
    final state = controller.state;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: phoneController,
          keyboardType: TextInputType.phone,
          autofillHints: const [AutofillHints.telephoneNumber],
          enabled: !state.isBusy,
          decoration: InputDecoration(
            labelText: 'Mobile number',
            hintText: '0917 123 4567',
            prefixIcon: const Icon(Icons.phone_iphone_rounded),
            errorText: state.phoneFieldError,
          ),
          onSubmitted: (_) => controller.requestOtp(phoneController.text),
        ),
        Spacing.gapXl,
        PrimaryButton(
          label: 'Send code',
          isBusy: state.isBusy,
          onPressed: () => controller.requestOtp(phoneController.text),
        ),
      ],
    );
  }
}

class _CodeStep extends StatelessWidget {
  const _CodeStep({required this.controller, required this.codeController});

  final AuthController controller;
  final TextEditingController codeController;

  @override
  Widget build(BuildContext context) {
    final state = controller.state;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: codeController,
          keyboardType: TextInputType.number,
          autofillHints: const [AutofillHints.oneTimeCode],
          enabled: !state.isBusy,
          decoration: InputDecoration(
            labelText: 'Verification code',
            prefixIcon: const Icon(Icons.password_rounded),
            errorText: state.codeFieldError,
          ),
          onSubmitted: (_) => controller.verifyOtp(codeController.text),
        ),
        Spacing.gapXl,
        PrimaryButton(
          label: 'Verify',
          isBusy: state.isBusy,
          onPressed: () => controller.verifyOtp(codeController.text),
        ),
        Spacing.gapMd,
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            TextButton(
              onPressed: state.isBusy ? null : controller.restartFlow,
              child: const Text('Change number'),
            ),
            TextButton(
              onPressed: state.isBusy ? null : controller.resendOtp,
              child: const Text('Resend code'),
            ),
          ],
        ),
      ],
    );
  }
}
