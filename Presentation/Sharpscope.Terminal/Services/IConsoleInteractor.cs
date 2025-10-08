namespace Sharpscope.Cli.Services;

public interface IConsoleInteractor
{
    void Info(string message);
    void Success(string message);
    void Error(string message);
    string AskText(string label, string? hint = null, bool allowEmpty = false);
    string AskChoice(string label, IEnumerable<string> choices, string defaultValue);
    void Raw(string text);
}
