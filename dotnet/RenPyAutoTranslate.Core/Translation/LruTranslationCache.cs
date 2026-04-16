namespace RenPyAutoTranslate.Core.Translation;

/// <summary>Simple LRU cache for (sourceLang, targetLang, text) → translated. Thread-safe for parallel translation workers.</summary>
public sealed class LruTranslationCache
{
    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly LinkedList<(string Key, string Value)> _order = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, string Value)>> _map = new();

    public LruTranslationCache(int capacity = 5000)
    {
        _capacity = Math.Max(16, capacity);
    }

    private static string MakeKey(string sourceLang, string targetLang, string text) =>
        $"{sourceLang}\u001f{targetLang}\u001f{text}";

    public bool TryGet(string sourceLang, string targetLang, string text, out string translated)
    {
        var key = MakeKey(sourceLang, targetLang, text);
        lock (_sync)
        {
            if (!_map.TryGetValue(key, out var node))
            {
                translated = "";
                return false;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            translated = node.Value.Value;
            return true;
        }
    }

    public void Set(string sourceLang, string targetLang, string text, string translated)
    {
        var key = MakeKey(sourceLang, targetLang, text);
        lock (_sync)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }

            var node = _order.AddFirst((key, translated));
            _map[key] = node;
            while (_map.Count > _capacity && _order.Last is not null)
            {
                var last = _order.Last;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
            }
        }
    }
}
