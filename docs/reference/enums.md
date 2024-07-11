# Enums

.NET `enum` types are marshalled as TypeScript-style
[numeric enums](https://www.typescriptlang.org/docs/handbook/enums.html#numeric-enums)
including [reverse mappings](https://www.typescriptlang.org/docs/handbook/enums.html#numeric-enums)
of enum member values to names.

```C#
[JSExport]
public enum ExampleEnum
{
    A = 1,
}
```

```JS
const a = ExampleEnum.A; // 1
const nameOfA = ExampleEnum[a]; // 'A'
```

(Enum members are not auto-camel-cased by the marshaller.)
