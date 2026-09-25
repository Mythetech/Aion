using Microsoft.JSInterop;
using NSubstitute;

namespace Aion.Test.TestDoubles;

/// <summary>
/// An <see cref="IJSRuntime"/> whose dynamic imports all return one substitute module, so services built on
/// JS modules (IndexedDB storage, PGlite) can run in tests and have their calls inspected.
/// </summary>
public sealed class JsModuleFake
{
    public IJSRuntime Runtime { get; } = Substitute.For<IJSRuntime>();
    public IJSObjectReference Module { get; } = Substitute.For<IJSObjectReference>();

    public JsModuleFake()
    {
        Runtime.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<IJSObjectReference>(Module));

        Returns("loadConnections", "[]");
        Returns("loadQueries", "[]");
        Returns("loadDatabaseMetas", "[]");
    }

    public void Returns(string function, string json)
    {
        Module.InvokeAsync<string>(function, Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<string>(json));
    }

    public IReadOnlyList<object?[]> CallsTo(string function) =>
        Module.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IJSObjectReference.InvokeAsync))
            .Select(c => c.GetArguments())
            .Where(args => args.Length == 2 && Equals(args[0], function))
            .Select(args => (object?[])(args[1] ?? Array.Empty<object?>()))
            .ToList();
}
