namespace Chatter.Rest.UriTemplates;

/// <summary>
/// A caller-supplied sequence wrapped so that it is read at most once — on first
/// enumeration, never before — and replayed from the recorded members every time
/// afterwards.
/// <para>
/// Deferring the read to the first enumeration is what puts a failure where template
/// order says it belongs. The first enumeration is <see cref="UriTemplateExpander"/>
/// enumerating the value at the expression that names it, so nothing runs ahead of the
/// expander and nothing can get in front of the failure the expander would have reported
/// itself. A value the expander never reaches — because an earlier variable in the same
/// expression failed, because an earlier expression failed, or because the template never
/// mentions the variable at all — is simply never read.
/// </para>
/// <para>
/// Recording the members is what stops the caller's sequence being read a second time.
/// A single-pass or lazily evaluated sequence would otherwise be drained by the first
/// expression that names it, and a mutable one could give two expressions of the same
/// template two different answers.
/// </para>
/// <para>
/// Members are validated during the read rather than after it, by the delegate the owner
/// supplies. Recording first and validating afterwards would keep reading past the first
/// invalid member, so a null followed by a throwing, blocking, or endless remainder would
/// never be reported.
/// </para>
/// </summary>
/// <typeparam name="T">
/// The member type: <see cref="KeyValuePair{TKey, TValue}"/> of <see cref="string"/> to
/// <see cref="string"/> for the associative-array shapes, <see cref="string"/> for a list.
/// </typeparam>
internal sealed class UriTemplateMemoizedSequence<T> : IEnumerable<T>
{
    private readonly string _name;
    private readonly Action<string, T> _validateMember;

    /// <summary>
    /// The caller's sequence, released the moment the read starts rather than when it
    /// finishes, so the sequence is reachable exactly once whether or not that read runs
    /// to completion.
    /// </summary>
    private IEnumerable<T>? _source;

    /// <summary>
    /// The recorded members: <see langword="null"/> until a read completes.
    /// </summary>
    private List<T>? _members;

    internal UriTemplateMemoizedSequence(string name, IEnumerable<T> source, Action<string, T> validateMember)
    {
        _name = name;
        _source = source;
        _validateMember = validateMember;
    }

    internal int Count => RecordedMembers().Count;

    internal void CopyTo(T[] array, int arrayIndex) => RecordedMembers().CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => RecordedMembers().GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the recorded members, reading the caller's sequence the first time and only
    /// the first time.
    /// </summary>
    private List<T> RecordedMembers()
    {
        if (_members is not null)
        {
            return _members;
        }

        var source = _source;

        if (source is null)
        {
            // Only reachable if a read threw and something enumerated the view again
            // afterwards. Expansion abandons the whole template on the first failure, so
            // nothing in this library does; reading the caller's sequence a second time
            // would break the one guarantee this type exists to make, so it says so
            // instead.
            throw new InvalidOperationException(
                $"The value of variable '{_name}' has already been read once and that read did not " +
                "complete, so its members were never recorded. A caller-supplied sequence is read at " +
                "most once.");
        }

        _source = null;

        var members = new List<T>();

        foreach (var member in source)
        {
            _validateMember(_name, member);

            members.Add(member);
        }

        _members = members;

        return members;
    }
}
