namespace Chatter.Rest.UriTemplates;

internal enum UriTemplateValueKind
{
    String,
    List,
    Dictionary
}

/// <summary>
/// A strongly-typed wrapper for URI template variable values.
/// Use the static factory methods to create instances.
/// </summary>
public sealed class UriTemplateValue
{
    internal UriTemplateValueKind Kind { get; }
    internal string? StringValue { get; }
    internal IReadOnlyList<string>? ListValue { get; }
    internal IReadOnlyDictionary<string, string>? DictionaryValue { get; }

    private UriTemplateValue(
        UriTemplateValueKind kind,
        string? stringValue,
        IReadOnlyList<string>? listValue,
        IReadOnlyDictionary<string, string>? dictionaryValue)
    {
        Kind = kind;
        StringValue = stringValue;
        ListValue = listValue;
        DictionaryValue = dictionaryValue;
    }

    /// <summary>
    /// Creates a <see cref="UriTemplateValue"/> representing a simple string value.
    /// </summary>
    /// <param name="value">The string value. Must not be null.</param>
    /// <returns>A new <see cref="UriTemplateValue"/> with <see cref="Kind"/> equal to <see cref="UriTemplateValueKind.String"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static UriTemplateValue FromString(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new UriTemplateValue(UriTemplateValueKind.String, value, null, null);
    }

    /// <summary>
    /// Creates a <see cref="UriTemplateValue"/> representing a list value.
    /// </summary>
    /// <param name="values">The list of string values. Must not be null, and no element may be null.</param>
    /// <returns>A new <see cref="UriTemplateValue"/> with <see cref="Kind"/> equal to <see cref="UriTemplateValueKind.List"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any element in <paramref name="values"/> is null.</exception>
    public static UriTemplateValue FromList(IEnumerable<string> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var list = new List<string>();
        foreach (var item in values)
        {
            if (item is null)
            {
                throw new ArgumentException("List must not contain null elements.", nameof(values));
            }
            list.Add(item);
        }

        return new UriTemplateValue(UriTemplateValueKind.List, null, list.AsReadOnly(), null);
    }

    /// <summary>
    /// Creates a <see cref="UriTemplateValue"/> representing an associative array (dictionary) value.
    /// </summary>
    /// <param name="pairs">The key-value pairs. Must not be null, and no key or value may be null.</param>
    /// <returns>A new <see cref="UriTemplateValue"/> with <see cref="Kind"/> equal to <see cref="UriTemplateValueKind.Dictionary"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pairs"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any key or value in <paramref name="pairs"/> is null.</exception>
    public static UriTemplateValue FromDictionary(IDictionary<string, string> pairs)
    {
        if (pairs is null)
        {
            throw new ArgumentNullException(nameof(pairs));
        }

        var dict = new Dictionary<string, string>();
        foreach (var kvp in pairs)
        {
            if (kvp.Key is null)
            {
                throw new ArgumentException("Dictionary must not contain null keys.", nameof(pairs));
            }
            if (kvp.Value is null)
            {
                throw new ArgumentException(
                    $"Dictionary must not contain null values (key: '{kvp.Key}').", nameof(pairs));
            }
            dict[kvp.Key] = kvp.Value;
        }

        return new UriTemplateValue(
            UriTemplateValueKind.Dictionary,
            null,
            null,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(dict));
    }
}
