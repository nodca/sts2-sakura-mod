using SakuraMod.TestProtocol;

namespace SakuraMod.RuntimeTests;

internal sealed class RuntimeAssertionCollector
{
    private readonly List<SakuraTestAssertion> _assertions = [];
    private readonly List<SakuraTestFailure> _failures = [];

    public IReadOnlyList<SakuraTestAssertion> Assertions => _assertions;
    public IReadOnlyList<SakuraTestFailure> Failures => _failures;
    public bool Passed => _assertions.All(assertion => assertion.Status == "PASS") && _failures.Count == 0;

    public void Equal<T>(string name, T expected, T actual)
    {
        var passed = EqualityComparer<T>.Default.Equals(expected, actual);
        _assertions.Add(new SakuraTestAssertion(
            name,
            passed ? "PASS" : "FAIL",
            Format(expected),
            Format(actual),
            passed ? null : "Values differ."));
    }

    public void True(string name, bool actual, string? detail = null) =>
        _assertions.Add(new SakuraTestAssertion(
            name,
            actual ? "PASS" : "FAIL",
            "True",
            actual.ToString(),
            actual ? null : detail));

    public void Check(string name, Action action)
    {
        try
        {
            action();
            _assertions.Add(new SakuraTestAssertion(name, "PASS", "no exception", "no exception"));
        }
        catch (Exception exception)
        {
            _assertions.Add(new SakuraTestAssertion(name, "FAIL", "no exception", exception.GetType().FullName, exception.Message));
            _failures.Add(new SakuraTestFailure(exception.GetType().FullName ?? exception.GetType().Name, exception.Message, exception.StackTrace));
        }
    }

    public void AddFailure(Exception exception) =>
        _failures.Add(new SakuraTestFailure(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            exception.StackTrace));

    private static string? Format<T>(T value) => value?.ToString();
}
