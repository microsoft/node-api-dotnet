using System;
using NodeApi;

namespace NodeApiTest;

[JSModule]
public class Binding
{
    private readonly Lazy<JSReference> _testObject = new(() => new JSReference(TestObject.Init()));
    private readonly Lazy<JSReference> _testObjectFreezeSeal = new(() => new JSReference(TestObjectFreezeSeal.Init()));

    public JSValue Object => _testObject.Value.GetValue() ?? JSValue.Undefined;
    public JSValue ObjectFreezeSeal => _testObjectFreezeSeal.Value.GetValue() ?? JSValue.Undefined;
}
