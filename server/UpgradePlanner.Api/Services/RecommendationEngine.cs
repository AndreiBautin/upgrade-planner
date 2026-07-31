using UpgradePlanner.Api.Models;

namespace UpgradePlanner.Api.Services;

public static class RecommendationEngine
{
    public record Result(bool IsBlocked, int EffectivePriority, int? UnlocksUpgradeId, string? UnlocksTitle);

    public static Dictionary<int, Result> Compute(List<Upgrade> upgrades)
    {
        var byId = upgrades.ToDictionary(u => u.Id);
        var childrenOf = upgrades
            .Where(u => u.PrerequisiteUpgradeId.HasValue)
            .GroupBy(u => u.PrerequisiteUpgradeId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var effective = new Dictionary<int, (int Priority, int SourceId)>();

        (int Priority, int SourceId) ComputeEffective(int id, HashSet<int> visiting)
        {
            if (effective.TryGetValue(id, out var cached)) return cached;
            // Guard against a corrupt/cyclic chain: treat the node as its own source rather than recursing forever.
            if (!visiting.Add(id)) return (byId[id].Priority, id);

            var best = (Priority: byId[id].Priority, SourceId: id);
            if (childrenOf.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    var childEffective = ComputeEffective(child.Id, visiting);
                    if (childEffective.Priority > best.Priority)
                    {
                        best = childEffective;
                    }
                }
            }

            visiting.Remove(id);
            effective[id] = best;
            return best;
        }

        foreach (var u in upgrades)
        {
            ComputeEffective(u.Id, new HashSet<int>());
        }

        var result = new Dictionary<int, Result>();
        foreach (var u in upgrades)
        {
            var isBlocked = u.PrerequisiteUpgradeId.HasValue
                && byId.TryGetValue(u.PrerequisiteUpgradeId.Value, out var prereq)
                && prereq.Status != UpgradeStatus.Purchased;

            var (priority, sourceId) = effective[u.Id];
            var unlocksId = sourceId == u.Id ? (int?)null : sourceId;
            var unlocksTitle = unlocksId.HasValue ? byId[unlocksId.Value].Title : null;

            result[u.Id] = new Result(isBlocked, priority, unlocksId, unlocksTitle);
        }

        return result;
    }
}
