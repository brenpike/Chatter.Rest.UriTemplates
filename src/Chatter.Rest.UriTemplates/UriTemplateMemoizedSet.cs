namespace Chatter.Rest.UriTemplates;

/// <summary>
/// An owned, order-preserving, lazily materialized view of a caller-supplied
/// <see cref="ISet{T}"/> of associative-array pairs. It is a replay of the entries that
/// were observed, not a general-purpose set.
/// <para>
/// It deliberately defines no equality relation of its own. A caller's set may be built
/// with a comparer finer than <see cref="EqualityComparer{T}.Default"/> — reference
/// identity, say — and may therefore legally hold two entries the default relation
/// considers equal. Copying such a set into a
/// <c>HashSet&lt;KeyValuePair&lt;string, string&gt;&gt;</c> would impose the default
/// relation on entries that were never subject to it and silently drop one of them,
/// changing the expanded URI. Backing the view with
/// <see cref="UriTemplateMemoizedSequence{T}"/> keeps every entry that was observed, in
/// the order it was observed, which is what the expander used to see when it enumerated
/// the caller's set directly.
/// </para>
/// <para>
/// It still implements <see cref="ISet{T}"/> because that is the type test
/// <see cref="UriTemplateExpander"/> dispatches on to decide a value has no
/// caller-defined order and must be canonicalized. A view arriving as a plain list would
/// take the expander's ordered-sequence branch instead, making the expansion depend on
/// the caller's set implementation — the very thing the set branch exists to prevent.
/// Preserving the interface is the point; the backing store is free to differ.
/// </para>
/// <para>
/// Nothing here reads the caller's set. The backing sequence materializes on its first
/// enumeration, which is the expander's, so wrapping a set costs nothing until — and
/// unless — an expression that names it is expanded.
/// </para>
/// <para>
/// The expander only ever enumerates the value, so enumeration, <see cref="Count"/>, and
/// <see cref="CopyTo"/> — none of which need an equality relation — are the only members
/// implemented. Every other member throws <see cref="NotSupportedException"/>: the
/// mutators because this is a replay of what was read, and the membership and
/// set-relation predicates because answering them would require the very relation this
/// type refuses to invent.
/// </para>
/// </summary>
internal sealed class UriTemplateMemoizedSet : ISet<KeyValuePair<string, string>>
{
    private readonly UriTemplateMemoizedSequence<KeyValuePair<string, string>> _entries;

    internal UriTemplateMemoizedSet(UriTemplateMemoizedSequence<KeyValuePair<string, string>> entries) =>
        _entries = entries;

    public int Count => _entries.Count;

    public bool IsReadOnly => true;

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _entries.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => _entries.CopyTo(array, arrayIndex);

    // Mutators: this is a replay of what was read, not a collection to write to.
    public bool Add(KeyValuePair<string, string> item) => throw Unsupported();

    void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) => throw Unsupported();

    public void Clear() => throw Unsupported();

    public bool Remove(KeyValuePair<string, string> item) => throw Unsupported();

    public void ExceptWith(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public void IntersectWith(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public void SymmetricExceptWith(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public void UnionWith(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    // Questions that only an equality relation could answer, and this type has none.
    public bool Contains(KeyValuePair<string, string> item) => throw Unsupported();

    public bool IsProperSubsetOf(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public bool IsProperSupersetOf(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public bool IsSubsetOf(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public bool IsSupersetOf(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public bool Overlaps(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    public bool SetEquals(IEnumerable<KeyValuePair<string, string>> other) => throw Unsupported();

    private static NotSupportedException Unsupported() =>
        new NotSupportedException(
            $"{nameof(UriTemplateMemoizedSet)} is a replay of the entries observed in a " +
            "caller-supplied set, not a general-purpose set. It supports enumeration only: it carries no " +
            "equality comparer, so it can answer neither membership nor set-relation questions, and it " +
            "cannot be modified.");
}
