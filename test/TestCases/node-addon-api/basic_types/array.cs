// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

public class TestBasicTypesArray : TestHelper, ITestObject
{
    private static JSValue CreateArray(JSCallbackArgs args)
        => (args.Length > 0) ? new JSArray((int)args[0]) : new JSArray();

    private static JSValue GetLength(JSCallbackArgs args)
        => ((JSArray)args[0]).Length;

    private static JSValue Get(JSCallbackArgs args)
        => ((JSArray)args[0])[(int)args[1]];

    private static JSValue Set(JSCallbackArgs args)
    {
        var array = (JSArray)args[0];
        array[(int)args[1]] = args[2];
        return JSValue.Undefined;
    }

    public JSObject Init() => new()
    {
        Method(CreateArray),
        Method(GetLength),
        Method(Get),
        Method(Set),
    };
}
