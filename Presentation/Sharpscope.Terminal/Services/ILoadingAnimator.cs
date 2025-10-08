namespace Sharpscope.Cli.Services;

public interface ILoadingAnimator
{
    Task StartAsync(string label, CancellationToken token);
}
