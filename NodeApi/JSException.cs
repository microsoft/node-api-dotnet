using System;

namespace NodeApi;

public class JSException : Exception
{
  public override string Message => ErrorInfo.Message;

  public JSErrorInfo ErrorInfo { get; }

  public JSException(JSErrorInfo errorInfo) =>  ErrorInfo = errorInfo;

  public unsafe JSException(string message)
  {
    ErrorInfo = new JSErrorInfo(message, JSStatus.GenericFailure);
  }
}
