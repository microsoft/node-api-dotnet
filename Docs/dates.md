# Date and time types in .NET and JavaScript

## JS Date / .NET DateTime & DateTimeOffset
There is not a clean mapping between the built-in types for dates and times in .NET and JS.
In particular the built-in JavaScript `Date` class has very limited and somewhat confusing
functionality. For this reason many applications use the popular `moment.js` library, but this
project prefers to avoid depending on an external library. Also [a new "Temporal" API is
proposed for standardization](https://tc39.es/proposal-temporal/docs/), but it is not widely
available yet.

A JavaScript [`Date`](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Date)
object is fundamentally a wrapper around a single primitive numeric value that is a UTC timestamp
(milliseconds since the epoch). It does not hold any other state related to offsets or time zones.
This UTC primitive value is returned by the `Date.valueOf()` function, and is one type of value
accepted by the `Date()` constructor. The confusing thing about it is that other constructors and
most `Date` methods operate in the context of the current local time zone, automatically converting
to/from UTC as needed. That includes the default `toString()` method, though alternatives like
`toUTCString()` and `toISOString()` can get the time in UTC instead.

In .NET, both `DateTime` and `DateTimeOffset` are widely used. While the latter is more modern
and fully-featured, the simpler `DateTime` is still sufficient for many scenarios. So for best
interoperability, both types of .NET values are convertible to and from JS `Date` values. This is
accomplished by adding either a `kind` or `offset` property to a regular `Date` object.

```TypeScript
type DateTime = Date | { kind?: 'utc' | 'local' | 'unspecified' }
```

When a .NET `DateTime` is marshalled to a JS `Date`, the date's UTC timestamp value becomes the
JS `Date` value, regardless of the `DateTime.Kind` property. Then the `kind` property is added to
facilitate consistent round-tripping, so that a `DateTime` can be passed from .NET to JS and
back to .NET without its `Kind` changing. (As noted above, the JS `Date.toString()` always
displays local time, and that remains true even for a `Date` with `kind == 'utc'`.) If a
regular JS `Date` object without a `kind` hint is marshalled to .NET, it becomes a `DateTime`
with `Utc` kind. (Defaulting to `Unspecified` would be more likely to result in undesirable
conversions to/from local-time.)

```TypeScript
type DateTimeOffset = Date | { offset?: number }
```

When a .NET `DateTimeOffset` is marshalled to a JS `Date`, the UTC timestamp value _without the
offset_ becomes the JS `Date` value. Then the `offset` property is added to the object. The
`offset` is a positive or negative integer number of minutes, equivalent to
[DateTimeOffset.TotalOffsetMinumtes](https://learn.microsoft.com/en-us/dotnet/api/system.datetimeoffset.totaloffsetminutes).
Additionally, the `toString()` method of the `Date` object is overridden such that it displays
the time _with the offset applied_, followed by the offset, in the form
`YYYY-MM-DD HH:mm:SS (+/-)HH:mm`, consistent with how .NET `DateTimeOffset` is displayed.
If a regular JS `Date` object without an `offset` value is marshalled to .NET, it becomes a
`DateTimeOffset` with zero offset (not local time-zone offset).

## JS number / .NET TimeSpan
JavaScript lacks a built-in type for representing time spans (at least until the "Temporal" API
is standardized). The common practice is to represent basic time spans as a number of milliseconds.
So a .NET `TimeSpan` is marshalled to or from a simple JS `number` value.

Note the `Date.offset` property introduced above is intentionally NOT a millisecond timespan value.
It is a whole (positive or negative) number of minutes, because `DateTimeOffset` does not support
second or millisecond precision for offsets.
