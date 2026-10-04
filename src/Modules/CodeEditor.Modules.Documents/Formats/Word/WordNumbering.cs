using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Word lists for reading: whether a level is bulleted or numbered, and the next item's number. As in Word, counting
/// follows the list definition (<c>abstractNum</c>): instances (<c>num</c>) of one definition continue a shared count,
/// and an instance with a start override (<c>startOverride</c>) restarts it on its first item. An item resets the
/// counters of deeper levels.
/// </summary>
internal sealed class WordNumbering
{
    private const int Levels = 9;

    private readonly Dictionary<int, ListInstance> _instances;
    private readonly Dictionary<int, int[]> _counters = [];
    private readonly HashSet<int> _started = [];

    private WordNumbering(Dictionary<int, ListInstance> instances) => _instances = instances;

    public static WordNumbering Load(MainDocumentPart main)
    {
        var numbering = main.NumberingDefinitionsPart?.Numbering;
        var abstracts = new Dictionary<int, AbstractNum>();
        foreach (var definition in numbering?.Elements<AbstractNum>() ?? [])
        {
            if (definition.AbstractNumberId?.Value is { } id)
            {
                abstracts.TryAdd(id, definition);
            }
        }

        var instances = new Dictionary<int, ListInstance>();
        foreach (var instance in numbering?.Elements<NumberingInstance>() ?? [])
        {
            if (instance.NumberID?.Value is { } numberId && instance.AbstractNumId?.Val?.Value is { } abstractId && abstracts.TryGetValue(abstractId, out var definition))
            {
                instances[numberId] = Describe(abstractId, definition, instance);
            }
        }

        return new WordNumbering(instances);
    }

    /// <summary>The next list item, or <c>null</c> if there is no such list (numId 0 removes numbering).</summary>
    public (bool IsOrdered, int Number)? Next(int numberId, int level)
    {
        if (!_instances.TryGetValue(numberId, out var instance))
        {
            return null;
        }

        var index = Math.Clamp(level, 0, Levels - 1);
        var counters = Counters(instance.AbstractId);
        if (_started.Add(numberId))
        {
            foreach (var restarted in instance.RestartedLevels)
            {
                counters[restarted] = 0;
            }
        }

        // The counter is how many items the level has had; the number counts from the level's start value.
        counters[index]++;
        Array.Clear(counters, index + 1, Levels - index - 1);
        var current = instance.Levels[index];
        return (current.IsOrdered, current.Start + counters[index] - 1);
    }

    private int[] Counters(int abstractId)
    {
        if (!_counters.TryGetValue(abstractId, out var counters))
        {
            counters = new int[Levels];
            _counters[abstractId] = counters;
        }

        return counters;
    }

    private static ListInstance Describe(int abstractId, AbstractNum definition, NumberingInstance instance)
    {
        var levels = Enumerable.Repeat((IsOrdered: false, Start: 1), Levels).ToArray();
        foreach (var level in definition.Elements<Level>())
        {
            if (level.LevelIndex?.Value is { } index and >= 0 and < Levels)
            {
                var format = level.NumberingFormat?.Val?.Value;
                var ordered = format is { } value && value != NumberFormatValues.Bullet && value != NumberFormatValues.None;
                levels[index] = (ordered, level.StartNumberingValue?.Val?.Value ?? 1);
            }
        }

        var restarted = new List<int>();
        foreach (var levelOverride in instance.Elements<LevelOverride>())
        {
            if (levelOverride.LevelIndex?.Value is { } index and >= 0 and < Levels && levelOverride.StartOverrideNumberingValue?.Val?.Value is { } start)
            {
                levels[index] = levels[index] with { Start = start };
                restarted.Add(index);
            }
        }

        return new ListInstance(abstractId, levels, [.. restarted]);
    }

    /// <param name="RestartedLevels">Levels the instance restarts.</param>
    private sealed record ListInstance(int AbstractId, (bool IsOrdered, int Start)[] Levels, int[] RestartedLevels);
}
