using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public record struct JSErrorInfo(string Message, JSStatus Status);
