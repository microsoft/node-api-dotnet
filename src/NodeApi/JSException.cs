// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi;

public class JSException : Exception
{
    public JSException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public JSException(JSError error) : base(error.Message)
    {
        Error = error;
    }

    public JSError? Error { get; }
}
