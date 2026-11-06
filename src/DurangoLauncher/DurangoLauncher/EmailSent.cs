namespace DurangoLauncher;

public sealed record EmailSent(string EmailId, string Email, int ExpiresIn, int ResendIn);
