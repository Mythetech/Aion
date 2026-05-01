using System.Text.RegularExpressions;
using Aion.Contracts.Queries;

namespace Aion.Core.Database.PostgreSQL;

public partial class PostgreSqlPlanParser
{
    [GeneratedRegex(@"^(\s*(?:->\s*)?)(.*?)\s+\(cost=(\d+\.?\d*)\.\.(\d+\.?\d*)\s+rows=(\d+)\s+width=(\d+)\)(.*)$")]
    private static partial Regex NodeLineRegex();

    [GeneratedRegex(@"\(actual time=(\d+\.?\d*)\.\.(\d+\.?\d*)\s+rows=(\d+)\s+loops=(\d+)\)")]
    private static partial Regex ActualStatsRegex();

    [GeneratedRegex(@"^(\s+)(\w[\w\s]*?):\s+(.+)$")]
    private static partial Regex PropertyLineRegex();

    public QueryPlanTree? Parse(QueryPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.PlanContent) || plan.PlanContent.StartsWith("Error"))
            return null;

        var lines = plan.PlanContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return null;

        var root = ParseNodes(lines);
        if (root == null)
            return null;

        return new QueryPlanTree
        {
            Root = root,
            TotalCost = root.TotalCost,
            SourceFormat = plan.PlanFormat,
            RawContent = plan.PlanContent
        };
    }

    private QueryPlanNode? ParseNodes(string[] lines)
    {
        var stack = new Stack<(int Indent, QueryPlanNode Node)>();
        QueryPlanNode? root = null;
        QueryPlanNode? lastNode = null;

        foreach (var line in lines)
        {
            var nodeMatch = NodeLineRegex().Match(line);
            if (nodeMatch.Success)
            {
                var indent = GetEffectiveIndent(nodeMatch.Groups[1].Value);
                var node = CreateNodeFromMatch(nodeMatch);

                if (root == null)
                {
                    root = node;
                    stack.Push((indent, node));
                }
                else
                {
                    while (stack.Count > 0 && stack.Peek().Indent >= indent)
                        stack.Pop();

                    if (stack.Count > 0)
                        stack.Peek().Node.Children.Add(node);

                    stack.Push((indent, node));
                }

                lastNode = node;
                continue;
            }

            var propMatch = PropertyLineRegex().Match(line);
            if (propMatch.Success && lastNode != null)
            {
                ApplyProperty(lastNode, propMatch.Groups[2].Value.Trim(), propMatch.Groups[3].Value.Trim());
            }
        }

        return root;
    }

    private static int GetEffectiveIndent(string prefix)
    {
        var raw = prefix.Replace("->", "  ");
        return raw.Length;
    }

    private QueryPlanNode CreateNodeFromMatch(Match match)
    {
        var operationAndTarget = match.Groups[2].Value.Trim();
        var (operation, target) = SplitOperationTarget(operationAndTarget);
        var remainder = match.Groups[7].Value.Trim();

        var node = new QueryPlanNode
        {
            Operation = operation,
            Target = target,
            StartupCost = double.Parse(match.Groups[3].Value),
            TotalCost = double.Parse(match.Groups[4].Value),
            EstimatedRows = long.Parse(match.Groups[5].Value),
        };

        var actualMatch = ActualStatsRegex().Match(remainder);
        if (actualMatch.Success)
        {
            node.ActualTime = double.Parse(actualMatch.Groups[2].Value);
            node.ActualRows = long.Parse(actualMatch.Groups[3].Value);
            node.Loops = int.Parse(actualMatch.Groups[4].Value);
        }

        return node;
    }

    private static (string Operation, string? Target) SplitOperationTarget(string text)
    {
        var onIndex = text.IndexOf(" on ", StringComparison.Ordinal);
        if (onIndex > 0)
        {
            return (text[..onIndex], text[(onIndex + 4)..]);
        }

        return (text, null);
    }

    private static void ApplyProperty(QueryPlanNode node, string key, string value)
    {
        switch (key)
        {
            case "Filter":
                node.Filter = value;
                break;
            case "Hash Cond" or "Join Filter" or "Merge Cond":
                node.JoinCondition = value;
                break;
            case "Sort Key":
                node.SortKey = value;
                break;
            default:
                node.Properties[key] = value;
                break;
        }
    }
}
