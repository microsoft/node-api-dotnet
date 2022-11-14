namespace NodeApi;

public enum Status : int
{
	OK = 0,
	InvalidArg,
	ObjectExpected,
	StringExpected,
	NameExpected,
	FunctionExpected,
	NumberExpected,
	BooleanExpected,
	ArrayExpected,
	GenericFailure,
	PendingException,
	Cancelled,
	EscapeCalledTwice,
	HandleScopeMismatch,
	CallbackScopeMismatch,
	QueueFull,
	Closing,
	BigIntExpected,
	DateExpected,
	ArrayBufferExpected,
	DetachableArrayBufferExpected,
	WouldDeadlock,
}
