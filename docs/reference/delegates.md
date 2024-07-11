# Delegates

An exported .NET delegate type is converted to a TypeScript function type definition:
```C#
[JSExport]
public delegate string ExampleCallback(int arg1, bool arg2);
```
```TS
export function ExampleCallback(arg1: number, arg2: boolean): string;
```

Then a JavaScript function can be passed to a .NET API that expects a delegate of that type, and
the parameters and return value will be marshalled accordingly.

```C#
[JSExport]
public static class Example
{
    public static void RegisterCallback(ExampleCallback cb) { … }
}
```
```JS
Example.registerCallback((arg1, arg2) => 'ok');
```

This is one way for .NET to call back into JavaScript. Another way is to
[implement a .NET interface with a JavaScript class](./classes-interfaces#implement-a-net-interface-with-a-js-class).
