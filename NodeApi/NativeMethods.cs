using System;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.CallingConvention;
using static System.Runtime.InteropServices.UnmanagedType;

namespace NodeApi;

#pragma warning disable CS0649 // Unused internal fields

internal static class NativeMethods
{
	static NativeMethods()
	{
		// Node APIs are all imported from the main `node` executable. Overriding the import
		// resolution is more efficient and avoids issues with library search paths and
		// differences in the name of the executable.
		NativeLibrary.SetDllImportResolver(
			typeof(NativeMethods).Assembly,
			(libraryName, assembly, searchPath) =>
			{
				return libraryName == "node"? NativeLibrary.GetMainProgramHandle() : default;
			});
	}

	// APIs defined here correspond to NAPI_VERSION 8.
	public const int Version = 8;

	[DllImport("node", EntryPoint = "napi_create_reference", CallingConvention = Cdecl)]
	public static extern Status CreateReference(
		nint env,
		nint value,
		uint initialRefCount,
		out nint result);

	[DllImport("node", EntryPoint = "napi_delete_reference", CallingConvention = Cdecl)]
	public static extern Status DeleteReference(
		nint env,
		nint value);

	[DllImport("node", EntryPoint = "napi_get_reference_value", CallingConvention = Cdecl)]
	public static extern Status GetReferenceValue(
		nint env,
		nint value,
		out nint result);

	[DllImport("node", EntryPoint = "napi_typeof", CallingConvention = Cdecl)]
	public static extern Status GetValueType(
		nint env,
		nint value,
		out ValueType result);

	[DllImport("node", EntryPoint = "napi_get_boolean", CallingConvention = Cdecl)]
	public static extern Status GetBoolean(
		nint env,
		[MarshalAs(I1)] bool value,
		out nint result);

	[DllImport("node", EntryPoint = "napi_get_value_bool", CallingConvention = Cdecl)]
	public static extern Status GetValueBoolean(
		nint env,
		nint value,
		[MarshalAs(I1)] out bool result);

	[DllImport("node", EntryPoint = "napi_create_string_utf16", CallingConvention = Cdecl)]
	public static extern Status CreateString(
		nint env,
		[MarshalAs(LPWStr)] string value,
		nuint length,
		out nint result);

	[DllImport("node", EntryPoint = "napi_get_value_string_utf16", CallingConvention = Cdecl)]
	public static extern Status GetValueString(
		nint env,
		nint value,
		[In, Out, MarshalAs(LPArray)] char[]? buf,
		nuint bufSize,
		out nuint result);

	[DllImport("node", EntryPoint = "napi_get_cb_info", CallingConvention = Cdecl)]
	public static extern Status GetArguments(
		nint env,
		nint args,
		ref nuint argc,
		[MarshalAs(LPArray)] nint[] argv,
		out nint thisArg,
		out nint data);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate nint Callback(nint env, nint args);

	public struct PropertyDescriptor
	{
		 // Exactly one of Utf8Name or Name should be specified.
		 [MarshalAs(LPUTF8Str)] public string? Utf8Name;
		 public nint Name;
		 [MarshalAs(FunctionPtr)] public Callback? Method;
		 [MarshalAs(FunctionPtr)] public Callback? Getter;
		 [MarshalAs(FunctionPtr)] public Callback? Setter;
		 public nint Value;
		 public PropertyAttributes Attributes;
		 public nint Data;
	}

	[DllImport("node", EntryPoint = "napi_define_properties", CallingConvention = Cdecl)]
	public static extern Status DefineProperties(
		nint env,
		nint obj,
		nuint propertyCount,
		[MarshalAs(LPArray)] PropertyDescriptor[] properties);

	[DllImport("node", EntryPoint = "napi_define_class", CallingConvention = Cdecl)]
	public static extern Status DefineClass(
		nint env,
		[MarshalAs(LPUTF8Str)] string name,
		nuint nameLength,
		[MarshalAs(FunctionPtr)] Callback constructor,
		nint data,
		nuint propertyCount,
		[MarshalAs(LPArray)] PropertyDescriptor[] properties,
		out nint result);

	[DllImport("node", EntryPoint = "napi_wrap", CallingConvention = Cdecl)]
	public static extern Status Wrap(
		nint env,
		nint jsObj,
		nint nativeObj,
		[MarshalAs(FunctionPtr)] Callback? finalizer,
		nint finalizeHint,
		nint outReference); // A finalizer must be supplied when requesting a reference.

	[DllImport("node", EntryPoint = "napi_unwrap", CallingConvention = Cdecl)]
	public static extern Status Unwrap(
		nint env,
		nint jsObj,
		out nint result);

	[DllImport("node", EntryPoint = "napi_remove_wrap", CallingConvention = Cdecl)]
	public static extern Status RemoveWrap(
		nint env,
		nint jsObj,
		out nint result);

	public struct ExtendedErrorInfo
	{
		[MarshalAs(LPUTF8Str)] public string Message;
		public nint EngineReserved;
		public uint EngineErrorCode;
		public Status ErrorCode;
	}

	[DllImport("node", EntryPoint = "napi_get_last_error_info", CallingConvention = Cdecl)]
	public static extern Status GetLastErrorInfo(
		nint env,
		out nint result); // Result must be marshalled separately because AOT doesn't support it.

	public static void ThrowIfNotOK(Status status)
	{
		if (status != Status.OK)
		{
			string? message = null;
			if (status == Status.PendingException)
			{
				var errorInfoStatus = GetLastErrorInfo(Env.Current, out var errorInfoPtr);
				if (errorInfoStatus == Status.OK)
				{
					var errorInfo = Marshal.PtrToStructure<ExtendedErrorInfo>(errorInfoPtr);
					message = errorInfo.Message;
				}
			}

			if (string.IsNullOrEmpty(message))
			{
				message = "Node API returned status " + status;
			}

			// TODO: Custom exception subclass.
			throw new Exception(message);
		}
	}
}
