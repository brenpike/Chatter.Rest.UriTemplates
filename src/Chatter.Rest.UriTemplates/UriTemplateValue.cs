namespace Chatter.Rest.UriTemplates;

/// <summary>
/// A strongly-typed wrapper for URI template variable values.
/// Use the static factory methods to create instances.
/// </summary>
public abstract class UriTemplateValue
{
    private protected UriTemplateValue() { }

    /// <summary>
    /// Creates a <see cref="StringValue"/> representing a simple string value.
    /// </summary>
    /// <param name="value">The string value. Must not be null.</param>
    /// <returns>A new <see cref="StringValue"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static StringValue From(string value) => new StringValue(value);

    /// <summary>
    /// Creates a <see cref="ListValue"/> representing a list value.
    /// </summary>
    /// <param name="values">The list of string values. Must not be null, and no element may be null.</param>
    /// <returns>A new <see cref="ListValue"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any element in <paramref name="values"/> is null.</exception>
    public static ListValue From(IEnumerable<string> values) => new ListValue(values);

    /// <summary>
    /// Creates a <see cref="DictionaryValue"/> representing an associative array (dictionary) value.
    /// </summary>
    /// <param name="pairs">The key-value pairs. Must not be null, and no key or value may be null.</param>
    /// <returns>A new <see cref="DictionaryValue"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pairs"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any key or value in <paramref name="pairs"/> is null.</exception>
    public static DictionaryValue From(IDictionary<string, string> pairs) => new DictionaryValue(pairs);

    internal abstract object? ToRawValue();
}

public sealed class StringValue : UriTemplateValue
{
    internal string Value { get; }
    internal StringValue(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        Value = value;
    }

    internal override object? ToRawValue() => Value;
}

public sealed class ListValue : UriTemplateValue
{
    internal IReadOnlyList<string> Values { get; }
    internal ListValue(IEnumerable<string> values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        var list = new List<string>();
        foreach (var item in values)
        {
            if (item is null) throw new ArgumentException("List must not contain null elements.", nameof(values));
            list.Add(item);
        }
        Values = list.AsReadOnly();
    }

    internal override object? ToRawValue() => new List<string>(Values);
}

public sealed class DictionaryValue : UriTemplateValue
{
    internal IReadOnlyDictionary<string, string> Pairs { get; }
    internal DictionaryValue(IDictionary<string, string> pairs)
    {
        if (pairs is null) throw new ArgumentNullException(nameof(pairs));
        var dict = new Dictionary<string, string>();
        foreach (var kvp in pairs)
        {
            if (kvp.Key is null) throw new ArgumentException("Dictionary must not contain null keys.", nameof(pairs));
            if (kvp.Value is null) throw new ArgumentException($"Dictionary must not contain null values (key: '{kvp.Key}').", nameof(pairs));
            dict[kvp.Key] = kvp.Value;
        }
        Pairs = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(dict);
    }

    internal override object? ToRawValue() => new Dictionary<string, string>(Pairs);
}
