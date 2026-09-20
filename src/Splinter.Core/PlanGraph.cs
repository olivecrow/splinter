namespace Splinter.Core;

public sealed record PlanTask(string TaskId, IReadOnlyList<string> DependsOn);
public sealed record PlanIssue(string Code, string TaskId, string Message);
public sealed record PlanGraphResult(
    IReadOnlyList<PlanIssue> Issues,
    IReadOnlyList<IReadOnlyList<string>> ExecutionLayers)
{
    public bool IsValid => Issues.Count == 0;
}

public static class PlanGraph
{
    public static PlanGraphResult Validate(IReadOnlyList<PlanTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        List<PlanIssue> issues = [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.TaskId))
                issues.Add(new("InvalidTaskId", task.TaskId, "Task IDs must not be empty."));
            else if (!ids.Add(task.TaskId))
                issues.Add(new("DuplicateTaskId", task.TaskId, "Task IDs must be unique."));
        }

        foreach (var task in tasks)
        {
            foreach (var dependency in task.DependsOn)
            {
                if (!ids.Contains(dependency))
                    issues.Add(new("MissingDependency", task.TaskId, $"Dependency '{dependency}' does not exist."));
            }
            if (task.DependsOn.Distinct(StringComparer.Ordinal).Count() != task.DependsOn.Count)
                issues.Add(new("DuplicateDependency", task.TaskId, "Dependencies must be unique."));
        }

        if (issues.Count != 0)
            return new(issues, []);

        // Readiness follows dependencies, never the declaration order of tasks.
        var remaining = tasks.ToList();
        var completed = new HashSet<string>(StringComparer.Ordinal);
        List<IReadOnlyList<string>> layers = [];
        while (remaining.Count != 0)
        {
            var ready = remaining.Where(task => task.DependsOn.All(completed.Contains)).ToArray();
            if (ready.Length == 0)
            {
                issues.Add(new("DependencyCycle", remaining[0].TaskId,
                    "The remaining graph contains a dependency cycle."));
                return new(issues, []);
            }
            layers.Add(ready.Select(task => task.TaskId).ToArray());
            foreach (var task in ready)
            {
                completed.Add(task.TaskId);
                remaining.Remove(task);
            }
        }
        return new(issues, layers);
    }
}
