namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Describes a single variable specification within a URI template expression,
/// including its name, optional prefix length modifier, and explode modifier.
/// </summary>
public sealed record UriTemplateVarSpec(string Name, int? PrefixLength, bool Explode);
