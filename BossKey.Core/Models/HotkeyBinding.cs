using BossKey.Core.Native;

namespace BossKey.Core.Models;

public sealed class HotkeyBinding
{
    public List<int> Keys { get; set; } = [];

    public bool IsValid => GetNormalizedKeys().Count > 0;

    public static HotkeyBinding FromKeys(IEnumerable<int> keys)
    {
        return new HotkeyBinding
        {
            Keys = keys
                .Select(VirtualKeyCodes.Normalize)
                .Where(static key => key > 0)
                .Distinct()
                .Order()
                .ToList()
        };
    }

    public HashSet<int> GetNormalizedKeys()
    {
        var result = new HashSet<int>();
        foreach (var key in Keys)
        {
            var normalized = VirtualKeyCodes.Normalize(key);
            if (normalized > 0)
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}
