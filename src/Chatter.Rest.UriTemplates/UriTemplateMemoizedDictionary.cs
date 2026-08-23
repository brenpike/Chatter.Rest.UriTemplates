namespace Chatter.Rest.UriTemplates;

/// <summary>
/// An owned, order-preserving, lazily materialized view of a caller-supplied
/// <see cref="IDictionary{TKey, TValue}"/> of associative-array pairs. It is a replay of
/// the entries that were observed, not a general-purpose dictionary.
/// <para>
/// The counterpart of <see cref="UriTemplateMemoizedSet"/>, and it exists for the same
/// reason. Copying into a <c>Dictionary&lt;string, string&gt;</c> — even an ordinal one —
/// imposes that dictionary's key equality on entries that were never subject to it. A
/// caller's dictionary may be built with a comparer finer than
/// <see cref="StringComparer.Ordinal"/>, reference identity for instance, and may
/// therefore legally hold two entries whose keys have identical text but are distinct
/// instances. Assigning both into an ordinal dictionary silently collapses them and the
/// expanded URI loses a member, where the expander had enumerated and canonicalized
/// both. Backing the view with <see cref="UriTemplateMemoizedSequence{T}"/> keeps every
/// entry that was observed, in the order it was observed.
/// </para>
/// <para>
/// It still implements <see cref="IDictionary{TKey, TValue}"/> because that is the type
/// test <see cref="UriTemplateExpander"/> dispatches on first, to decide a value is a
/// keyed map with no caller-defined order and so must be canonicalized. A view arriving
/// as a plain list would fall through to the ordered-sequence branch and take the
/// caller's insertion order instead.
/// </para>
/// <para>
/// Nothing here reads the caller's dictionary. The backing sequence materializes on its
/// first enumeration, which is the expander's, so wrapping a dictionary costs nothing
/// until — and unless — an expression that names it is expanded.
/// </para>
/// <para>
/// The expander reaches a dictionary value exactly the way it reaches a set: its branch
/// hands the value to <c>ExpandAssociativeArray</c>, whose parameter is an
/// <see cref="IEnumerable{T}"/> of pairs and which only enumerates it. Nothing indexes
/// it, looks a key up in it, or reads its key or value collections. So enumeration,
/// <see cref="Count"/>, and <see cref="CopyTo"/> — the members that need no equality
/// relation — are the only ones implemented. Every other member throws
/// <see cref="NotSupportedException"/>: the mutators because the view is a replay of what
/// was read, and the key-addressed members because answering them would require the very
/// relation this type refuses to invent. <see cref="Keys"/> and <see cref="Values"/>
/// throw for a further reason: this view may hold two entries with textually equal keys,
/// so no honest <see cref="ICollection{T}"/> of keys exists for it.
/// </para>
/// </summary>
internal sealed class UriTemplateMemoizedDictionary : IDictionary<string, string>
{
    private readonly UriTemplateMemoizedSequence<KeyValuePair<string, string>> _entries;

    internal UriTemplateMemoizedDictionary(UriTemplateMemoizedSequence<KeyValuePair<string, string>> entries) =>
        _entries = entries;

    public int Count => _entries.Count;

    public bool IsReadOnly => true;

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _entries.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => _entries.CopyTo(array, arrayIndex);

    // Mutators: this is a replay of what was read, not a collection to write to.
    public string this[string key]
    {
        get => throw Unsupported();
        set => throw Unsupported();
    }

    public void Add(string key, string value) => throw Unsupported();

    public void Add(KeyValuePair<string, string> item) => throw Unsupported();

    public void Clear() => throw Unsupported();

    public bool Remove(string key) => throw Unsupported();

    public bool Remove(KeyValuePair<string, string> item) => throw Unsupported();

    // Key-addressed members: only an equality relation could answer these, and this type
    // has none. Keys and Values would additionally have to pretend the entries are
    // unique by key, which is exactly what this view exists not to assume.
    public ICollection<string> Keys => throw Unsupported();

    public ICollection<string> Values => throw Unsupported();

    public bool ContainsKey(string key) => throw Unsupported();

    public bool Contains(KeyValuePair<string, string> item) => throw Unsupported();

    public bool TryGetValue(string key, out string value) => throw Unsupported();

    private static NotSupportedException Unsupported() =>
        new NotSupportedException(
            $"{nameof(UriTemplateMemoizedDictionary)} is a replay of the entries observed in a " +
            "caller-supplied dictionary, not a general-purpose dictionary. It supports enumeration only: it " +
            "carries no key equality comparer, so it can answer no key-addressed question, and it cannot be " +
            "modified.");
}
