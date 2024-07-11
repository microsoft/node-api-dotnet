# .NET Generics in JavaScript

The JavaScript runtime type system lacks generics, so .NET generic types and methods are projected
into JavaScript using a special convention. The projections are suffixed with the dollar (`$`)
character, chosen because it is a valid identifier charater in JavaScript but not in C#, and
because it looks like an operator.

Note while TypeScript does have generics, they are merely a compile-time facade and do not have
any effect on runtime binding or execution. So it is unfortunately not possible in most cases
to directly map .NET generics to TypeScript generics. (The exception is when [mapping .NET generic
collection interfaces to TypeScript generic collections](./arrays-collections#collections) like
`Array<T>` and `Map<TKey, TValue>`, which does work.)

```JavaScript
// JavaScript
import dotnet from 'node-api-dotnet';
const System = dotnet.System;
System.Enum.Parse$(System.DayOfWeek)('Tuesday'); // Call generic method
System.Comparer$(System.DateTime).Create()       // Call static method on generic class
const TaskCompletionSourceOfDate = System.TaskCompletionSource$(System.DateTime);
new TaskCompletionSourceOfDate();                // Create instance of generic class
new System.TaskCompletionSource();               // Create instance of non-generic class
```

A .NET _generic method definition_ is projected as a function with a `$` suffix. That function
takes generic type parameter(s) and returns the _specialized_ generic function. So, .NET
`Enum.Parse<T>()` is projected as `Enum.Parse$(Type)`.

A .NET _generic type definition_ is also projected as a function with a `$` suffix. That function
takes generic type parameter(s) and returns the _specialized_ generic type. So, .NET `Comparer<T>`
is projected as `Comparer$(Type)`.

If a type has both generic and non-generic variants, the non-generic type is still available
normally, without any `$` suffix. If a type has multiple generic variants then the one `$` function
returns the requested type specialization according to the number of type arguments supplied.

## Getting a type full name
Calling the `toString()` method on the JS projection of any generic type definition, specialized
type, or non-generic type returns the full .NET type name. This may be helpful for diagnostics.

```JavaScript
// JavaScript
System.Comparer$.toString();                  // 'System.Comparer<T>'
System.Comparer$(System.DateTime).toString(); // 'System.Comparer<System.DateTime>
System.String.toString();                     // 'System.String'
```

## Static binding / AOT
The above applies to [dynamic binding](../scenarios/js-dotnet-dynamic), when the `node-api-dotnet`
library can use reflection to locate generic type and method definitions, specialize them, and
invoke them. But some of that is impossible to do in an
[ahead-of-time compiled environment](../features/dotnet-native-aot). Dynamically specifying generic
type arguments from JavaScript would require reflection and code-generation, whch are not supported
in an AOT executable. (In a pure C# application, the AOT compiler would be able to know exactly
what type arguments are used with any generic type or method, so it can generate specialized code
accordingly.)

This means that [C# Node API modules compiled as AOT](../scenarios/js-aot-module) cannot export
generic types or methods. They can still use generic types in properties or methods, when the type
argument is specified ahead of time. For example it is OK to use AOT to export a method that has a
parameter of type `KeyValuePair<string, int>`. But exporting a generic method with type parameter
`T` and method parameter `KeyValuePair<string, T>` will not work with AOT.
