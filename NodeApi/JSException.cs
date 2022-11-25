using System;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public class JSException : Exception
{
  public override string Message { get; }

  public unsafe JSException(napi_status status)
  {
    Message = status.ToString();
  }

  public unsafe JSException(string message)
  {
    Message = message;
  }
}
