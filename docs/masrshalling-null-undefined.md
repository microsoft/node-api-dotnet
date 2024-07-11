# Marshalling `null` and `undefined`

Consider:
 - JavaScript has both `null` and `undefined` primitive values, while .NET has
   only `null`.
 - A default/uninitialized value in JS is `undefined`, while in .NET the default
   is `null` (for reference types and `Nullable<T>` value types).
 - In JavaScript, `typeof undefined === 'undefined'` and `typeof null === 'object'`.

So how should these inconsistencies in type systems of the two platforms be
reconciled during automatic marshalling?

> Note: Regardless of any approach taken here, .NET code has the option to fall
back to working directly with `JSValue` and its distinct `JSValue.Null` and
`JSValue.Undefined` values.

## Marshalling .NET `null` to JavaScript
For discussion here, `T` may any _specific_ (not `object/any`) type, including both
marshal-by-value (number, struct, enum) and marshal-by-ref (string, class, interface)
types.

If `T` is not nullable (neither a `Nullable<T>` value type nor a nullable reference
type), then the TypeScript projection will allow neither `null` nor `undefined`.
However, **_marshalling must still handle null .NET reference values even when the
type is non-nullable_**.

In any case a JS `undefined` value passed to .NET is always converted to `null` by
the marshaller.

### Option A: .NET `null` -> JS `undefined`
If .NET `null` values are marshalled as JS `undefined`, that has the following effects:

| Description | C# API | JS API | JS Notes |
|-------------|--------|--------|----------|
| Method with optional param | `void Method(T? p = null)` | `method(p?: T): void` | `p` is `undefined` in JS if a .NET caller omitted the parameter _or_ supplied `null`;<br>`p` is never `null` in JS when called by .NET.
| Method with nullable param | `void Method(T? p)` | `method(p: T \| undefined): void` | `p` is `undefined` in JS if a .NET caller supplied `null`.
| Method with nullable return | `T? Method()` | `method(): T \| undefined` | Result is `undefined` in JS if .NET method returned `null`;<br>result is never `null` when returned by .NET.
| Nullable property | `T? Property` | `property?: T` | Property value is `undefined` (_but the property exists_) in JS if the object was passed from .NET;<br>value is never `null` (or missing) on an object from .NET.

### Option B: .NET `null` -> JS `null`
Alternatively, if .NET `null` values are marshalled as JS `null`, that has the following effects:

| Description | C# API | JS API | JS Notes |
|-------------|--------|--------|----------|
| Method with optional param | `void Method(T? p = null)` | `method(p: T \| null): void` | `p` is `null` in JS if a .NET caller omitted the parameter _or_ supplied `null`;<br>`p` is never `undefined` in JS when called by .NET.
| Method with nullable param | `void Method(T? p)` | `method(p?: T \| null): void` | `p` is `null` in JS if a .NET caller supplied `null`.
| Method with nullable return | `T? Method()` | `method(): T \| null` | Result is `null` in JS if .NET method returned `null`;<br>result is never `undefined` when returned by .NET.
| Nullable property | `T? Property` | `property: T \| null` | Property value is `null` (and the property exists) in JS if the object was passed from .NET;<br>value is never `undefined` (or missing) on an object from .NET.

## JavaScript `null` vs `undefined` practices
While `null` and `undefined` are often used interchangeably, the distinction can sometimes
be important. Let's analyze some ways in which `null` and/or `undefined` values might be
handled differently, and how common those practices are in JavaScript.

### Detecting optional parameters to a JavaScript function
In JavaScript, there are several common ways to detect when an optional
parameter was not supplied to a function:
```TS
function exampleFunction(optionalParameter?: any): void
```
1. Common / best practice: Check if the value type is equal to `'undefined'`.<br>
   `if (typeof optionalParameter === 'undefined')`

2. Somewhat common: Check if the value is strictly equal to `undefined`.<br>
   `if (optionalParameter === undefined)`

3. Common / best practice in TS & ES2020: Use the "nullish coalescing operator", which
   handles both `null` and `undefined`:<br>
   `value = optionalParameter ?? defaultValue`

4. Traditional and still common (occasionally error-prone): Check if the value is falsy.<br>
   `if (!optionalParameter)`<br>
   `value = optionalParameter || defaultValue`

5. Less common: Check the length of the `arguments` object. (Use of the special
  `arguments` object is [discouraged](
  https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Functions/arguments)
  in modern JS, in favor of rest parameters.)<br>
   `if (arguments.length === 0)`

6. Uncommon: Check if the value is null with _loose_ equality.
It handles both `null` and `undefined` because `null == undefined`. (The
loose equality operator is usually flagged by linters.)<br>
   `if (optionalParameter == null)`

| |A: null->undefined|B: null->null|
|-|:----------------:|:-----------:|
|1|✅|❌|
|2|✅|❌|
|3|✅|✅|
|4|✅|✅|
|5|❌|❌|
|6|✅|✅|

### Checking the return value of a function/method
A JavaScript function my return `undefined`, or `null`, when it yields no result.
There is [no strong consensus among the JS developer community](
https://stackoverflow.com/questions/37980559/is-it-better-to-return-undefined-or-null-from-a-javascript-function)
about when to use either one; some developers may prefer one or the other while
others may not think very hard about the distinction. There are a few ways the
caller might check the return value:
```TS
function exampleFunction(): any
```
1. Traditional and still common (occasionally error-prone): Check if the result value is
   falsy.<br>
   `if (!result)`
2. Common: Check if the result value type is `'undefined'` or value is strictly equal
   to `undefined`.<br>
   `if (typeof result === 'undefined')`<br>
   `if (result === undefined)`
3. Uncommon: Check if the result value is null with _loose_ equality.<br>
   `if (result == null)`

| |A: null->undefined|B: null->null|
|-|:----------------:|:-----------:|
|1|✅|✅|
|2|✅|❌|
|3|✅|✅|

### Detecting optional properties on a JavaScript object
In JavaScript, there are a few ways to detect when an optional property was
not supplied with an object:
```TS
interface Example {
    optionalProperty?: any;
}
```
1. Common: Use the `in` operator.<br>`if ('optionalProperty' in exampleObject)`
2. Common: Use `hasOwnProperty` or the more modern `hasOwn` replacement.<br>
   `if (!exampleObject.hasOwnProperty('optionalProperty'))`<br>
   `if (!Object.hasOwn(exampleObject, 'optionalProperty'))`
3. Traditional and still common (occasionally error-prone): Check if the property value
   is falsy.<br>
   `if (!exampleObject.optionalProperty)`<br>
   `if (!exampleObject['optionalProperty'])`<br>
   `value = exampleObject.optionalProperty || defaultValue`
4. Less common: Check if the property value type is `'undefined'` or value is
   strictly equal to `undefined`.<br>
   `if (typeof exampleObject.optionalProperty === 'undefined')`<br>
   `if (exampleObject.optionalProperty === undefined)`
5. Less common: Use the nullish coalescing operator<br>
   `value = exampleObject.optionalProperty ?? defaultValue`
6. Uncommon: Check if the property value is null with _loose_ equality.<br>
   `if (exampleObject.optionalProperty == null)`

| |A: null->undefined|B: null->null|
|-|:----------------:|:-----------:|
|1|❌|❌|
|2|❌|❌|
|3|✅|✅|
|4|✅|❌|
|5|✅|✅|
|6|✅|✅|

Note even when marshalling `null` to `undefined`, common checks that rely on the
_existince_ of properties can fail. And operations that enumerate the object properties
may have differing behavior for missing properties vs ones with `undefined` value. For
more on that subtle distinction, see [TypeScript's `--exactOptionalPropertyTypes` option](
https://devblogs.microsoft.com/typescript/announcing-typescript-4-4-beta/#exact-optional-property-types).

### Checking for strict `null` equality
JavaScript code _can_ specifically check if a parameter, return value, or
property value is strictly equal to null:
```JS
if (value === null)
```
|A: null->undefined|B: null->null|
|:----------------:|:-----------:|
|❌|✅|

More experienced JavaScript developers never write such code, since
they are aware of the pervasiveness of `undefined`. But it can be easy
for developers coming from other languages (like C# or Java) to write
such code while assuming `null` works the same way, or merely from
muscle memory.

### JS APIs with semantic differences between `undefined` vs `null`
A JavaScript API _could_ assign wholly different meanings to the two values, for
instance using `undefined` to represent an uninitialized state and `null` to
represent an intialized-but-cleared state. Since automatic marshalling of .NET
`null` cannot support that distinction, calling such a JS API from .NET would
require direct use of `JSValue.Undefined` and `JSValue.Null` (or perhaps a
JS wrapper for the targeted API) to handle the disambiguation. But such an API
design aspect would likely confuse many JavaScript developers as well, so it
is not a common occurrence.

## Design Choice
In the tables above, there are fewer ❌ marks in column A; this indicates
that mapping .NET `null` to JS `undefined` is the better choice for default
marshalling behavior.

There are a few rare cases in which the default may be problematic:
1. Omitted optional function parameters, when the JS function body checks
`arguments.length`.
2. Omitted optional properties of an object, when the JS code checks
whether the object has the property, or enumerates the object properties.
3. A nullable (not optional) value where the JS code checks for strict
null equality.

To handle these cases (and any other situations that might arise), we can add
flags to the ([planned](https://github.com/microsoft/node-api-dotnet/issues/64))
`[JSMarshalAs]` attribute to enable setting the null-value marshalling behavior
of a specific .NET method parameter, return value, or property to one of three
options:
  - `undefined` (default)
  - `null`
  - omit - Exclude from the function arguments (if there are no non-omitted
    arguments after it), or exclude from the properties of the marshalled object.
