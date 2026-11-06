namespace DurangoLauncher;

public sealed record EmailVerified(string? SignupToken, string? ResetToken, string? Username, string Email, string Purpose = "");
