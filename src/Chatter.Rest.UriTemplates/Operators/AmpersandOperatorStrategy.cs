namespace Chatter.Rest.UriTemplates;

internal sealed class AmpersandOperatorStrategy : IOperatorStrategy
{
    public string Prefix => "&";
    public string Separator => "&";
    public bool IsNamed => true;

    public string FormatEmpty(string varName) => varName + "=";

    public string FormatValue(string varName, string encodedValue) => varName + "=" + encodedValue;

    public string Encode(string value) => UriTemplateExpander.EncodeUnreserved(value);
}
