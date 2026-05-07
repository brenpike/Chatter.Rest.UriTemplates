namespace Chatter.Rest.UriTemplates;

internal sealed class PlusOperatorStrategy : IOperatorStrategy
{
    public string Prefix => "";
    public string Separator => ",";
    public bool IsNamed => false;

    public string FormatEmpty(string varName) => "";

    public string FormatValue(string varName, string encodedValue) => encodedValue;

    public string Encode(string value) => UriTemplateEncoder.EncodeReserved(value);
}
