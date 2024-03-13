# Ref and Out Parameters

JavaScript does not have any concept of `ref` or `out` parameters like C#. So the JS marshaller
and type-definitions generator apply a transformation to any .NET method that has `ref` or `out`
parameters.

## The "Try" pattern

If the .NET method follows the "Try" pattern (the method name starts with `"Try"`, returns `bool`,
and the last parameter is `out`), then in the JS method the `out` parameter is moved to the return
value and a `false` result becomes `undefined`.

```C#
public class DateTime
{
    public static bool TryParse(string? input, out DateTime result);
}
```

```TS
// Generated type definition
export class TimeSpan {
    static TryParse(input: string | undefined): number | undefined;
}
```

```JS
const timeSpan = dotnet.System.TimeSpan.TryParse(timeSpanString);
if (typeof timeSpan === 'undefined') {
    // parse failed
}
```

## Method with `out` parameters

If the .NET method has one or more `out` parameters but does not match the "Try" pattern above,
then the `out` parameters are omitted from the JS method and the return type is an object
with a `result` property for the return value (if the return type is not `void`) and additional
properties for each named `out` parameter.

```C#
[JSExport]
public static double GetAverage(
    IEnumerable<double> data,
    out double standardDeviation);
```

```TS
// Generated type definition
export function getAverage(data: Iterable<number>)
    : { result: number, standardDeviation: number };
```

```JS
const { result, standardDeviation } = getAverage(data);
```

## Method with `ref` parameters

If the .NET method has one or more `ref` parameters, then the `ref` paramaters are included both in
the JS method parameters and the result object. Note it may be necessary to explicitly assign the
output paramter value back to the original variable in order to achieve `ref` semantics.

```C#
[JSExport]
public static Memory<byte>? GetNextToken(Memory<byte> input, ref int position);
```

```TS
// Generated type definition
export function getNextToken(input: UInt8Array, position: number)
    : { result: UInt8Array | undefined, position: number };
```

```JS
const data = new UInt8Array(buffer, offset, length);
let nextPosition = 0;
while (true) {
    const { result, position } = getNextToken(data, nextPosition);
    if (!result) break;
    // (process next token result)
    nextPosition = position; // Assign the `ref` output value
}
```

If a method has both `ref` and `out` parameters, then the result object includes both the `ref` and
`out` parameters.

If one of the parameters is named `result`, then the return value property gets the name `_result`
to avoid a name conflict.
