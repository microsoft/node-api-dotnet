namespace Microsoft.JavaScript.NodeApi;

public enum JSValueType : int
{
    Undefined,
    Null,
    Boolean,
    Number,
    String,
    Symbol,
    Object,
    Function,
    External,
    BigInt,
}
