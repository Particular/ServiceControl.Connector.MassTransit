public interface IHealthCheckerProvider
{
    Task<(bool Success, string ErrorMessage)> TryCheck(CancellationToken cancellationToken);
}