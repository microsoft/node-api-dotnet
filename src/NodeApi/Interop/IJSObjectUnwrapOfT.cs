namespace NodeApi;

public interface IJSObjectUnwrap<T> where T : class
{
    static abstract T? Unwrap(JSCallbackArgs args);
}
