namespace Chatter.Rest.UriTemplates;

internal interface IOperatorStrategy
{
    string Prefix { get; }
    string Separator { get; }
    bool IsNamed { get; }
    string FormatEmpty(string varName);
    string FormatValue(string varName, string encodedValue);
    string Encode(string value);
}
