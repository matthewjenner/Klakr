using Klakr.Core.Input;

namespace Klakr.Core.Steps;

/// <summary>
/// Checks whether <see cref="WatchKey"/> is physically held right now and runs
/// <see cref="ThenSteps"/> if it is, otherwise <see cref="ElseSteps"/>.
/// </summary>
public sealed record ConditionalBranchStep : IStep
{
    /// <summary>The key whose held-state selects the branch.</summary>
    public Key WatchKey { get; init; }

    public IReadOnlyList<IStep> ThenSteps { get; init; } = [];
    public IReadOnlyList<IStep> ElseSteps { get; init; } = [];

    public async Task ExecuteAsync(ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<IStep> branch = ctx.KeyState.IsHeld(WatchKey) ? ThenSteps : ElseSteps;
        foreach (IStep child in branch)
            await child.ExecuteAsync(ctx, ct);
    }
}
