using System;
using System.Runtime.InteropServices;

namespace NodeApi;

#pragma warning disable CS0649 // Unused internal fields

internal static class NativeMethods
{
	// APIs defined here correspond to NAPI_VERSION 8.
	public const int Version = 8;

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate nint Callback(nint env, nint callbackInfo);

	public struct PropertyDescriptor
	{
		 // Exactly one of Utf8Name or Name should be specified.

		 [MarshalAs(UnmanagedType.LPUTF8Str)]
		 public string? Utf8Name;

		 public nint Name;

		 [MarshalAs(UnmanagedType.FunctionPtr)]
		 public Callback? Method;

		 [MarshalAs(UnmanagedType.FunctionPtr)]
		 public Callback? Getter;

		 [MarshalAs(UnmanagedType.FunctionPtr)]
		 public Callback? Setter;

		 public nint Value;

		 public PropertyAttributes Attributes;

		 public nint Data;
	}

	[DllImport("node.exe", EntryPoint = "napi_define_properties",
		CallingConvention = CallingConvention.Cdecl)]

	public static extern Status DefineProperties(
		nint env,
		nint obj,
		nuint propertyCount,
		[MarshalAs(UnmanagedType.LPArray)] PropertyDescriptor[] properties);

	public static void ThrowIfNotOK(Status status)
	{
		if (status != Status.OK)
		{
			// TODO: Custom exception subclass.
			throw new Exception("Node API returned status " + status);
		}
	}
}
