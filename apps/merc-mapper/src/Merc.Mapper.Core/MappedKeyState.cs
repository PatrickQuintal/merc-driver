namespace Merc.Mapper;

internal sealed class MappedKeyState
{
    private readonly Dictionary<ushort, HashSet<string>> _downSourcesByTarget = [];
    private readonly Dictionary<ushort, DateTimeOffset> _repeatDueByTarget = [];

    public KeyTransition Apply(ushort targetKey, string sourceId, bool down, bool repeatEnabled, DateTimeOffset now, int repeatDelayMs)
    {
        var shouldSend = false;
        var changed = false;

        if (down)
        {
            if (!_downSourcesByTarget.TryGetValue(targetKey, out var sources))
            {
                sources = [];
                _downSourcesByTarget[targetKey] = sources;
                shouldSend = true;
            }

            changed = sources.Add(sourceId);
            if (changed && repeatEnabled)
            {
                _repeatDueByTarget[targetKey] = now.AddMilliseconds(repeatDelayMs);
            }
        }
        else if (_downSourcesByTarget.TryGetValue(targetKey, out var sources) && sources.Remove(sourceId))
        {
            changed = true;
            if (sources.Count == 0)
            {
                _downSourcesByTarget.Remove(targetKey);
                _repeatDueByTarget.Remove(targetKey);
                shouldSend = true;
            }
        }

        return new KeyTransition(changed, shouldSend);
    }

    public void RollBackDown(ushort targetKey, string sourceId)
    {
        if (!_downSourcesByTarget.TryGetValue(targetKey, out var sources))
        {
            return;
        }

        sources.Remove(sourceId);
        if (sources.Count == 0)
        {
            _downSourcesByTarget.Remove(targetKey);
            _repeatDueByTarget.Remove(targetKey);
        }
    }

    public ushort[] ReleaseAll()
    {
        var keys = _downSourcesByTarget.Keys.ToArray();
        _downSourcesByTarget.Clear();
        _repeatDueByTarget.Clear();
        return keys;
    }

    public ushort[] TakeDueRepeats(DateTimeOffset now, int repeatRateMs)
    {
        if (_repeatDueByTarget.Count == 0)
        {
            return [];
        }

        var dueKeys = _repeatDueByTarget
            .Where(pair => pair.Value <= now && _downSourcesByTarget.ContainsKey(pair.Key))
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var key in dueKeys)
        {
            _repeatDueByTarget[key] = now.AddMilliseconds(repeatRateMs);
        }

        return dueKeys;
    }

    public bool IsDown(ushort targetKey)
    {
        return _downSourcesByTarget.ContainsKey(targetKey);
    }
}

internal readonly record struct KeyTransition(bool Changed, bool ShouldSend);
