using System;
using NodeApi;

namespace NodeApiTest;

//TODO: Implement error handling

public class TestArrayBuffer : TestHelper, ITestObject
{

    private static readonly int testLength = 4;

    private byte[] _testData = new byte[testLength];
    private int _finalizeCount = 0;

    private static void InitData(Span<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }
    }

    //bool VerifyData(uint8_t* data, size_t length) {
    //  for (size_t i = 0; i < length; i++) {
    //    if (data[i] != static_cast<uint8_t>(i)) {
    //      return false;
    //    }
    //  }
    //  return true;
    //}

    private static JSValue CreateBuffer(JSCallbackArgs args)
    {
        var buffer = new JSArrayBuffer(testLength);

        //    if (buffer.Length != testLength)
        //    {
        //        JSValue.CreateError("Incorrect buffer length.")
        //        .ThrowAsJavaScriptException();
        //return Value();
        //}

        InitData(buffer.Data);
        return buffer;
    }

    private JSValue CreateExternalBuffer(JSCallbackArgs args)
    {
        _finalizeCount = 0;

        var buffer = new JSArrayBuffer(new ReadOnlyMemory<byte>(_testData));

        //  if (buffer.ByteLength() != testLength) {
        //    Error::New(info.Env(), "Incorrect buffer length.")
        //        .ThrowAsJavaScriptException();
        //    return Value();
        //  }

        //  if (buffer.Data() != testData) {
        //    Error::New(info.Env(), "Incorrect buffer data.")
        //        .ThrowAsJavaScriptException();
        //    return Value();
        //  }

        InitData(_testData);
        return buffer;
    }

    //Value CreateExternalBufferWithFinalize(const CallbackInfo& info) {
    //  finalizeCount = 0;

    //  uint8_t* data = new uint8_t[testLength];

    //  ArrayBuffer buffer = ArrayBuffer::New(
    //      info.Env(), data, testLength, [](Env /*env*/, void* finalizeData) {
    //        delete[] static_cast<uint8_t*>(finalizeData);
    //        finalizeCount++;
    //      });

    //  if (buffer.ByteLength() != testLength) {
    //    Error::New(info.Env(), "Incorrect buffer length.")
    //        .ThrowAsJavaScriptException();
    //    return Value();
    //  }

    //  if (buffer.Data() != data) {
    //    Error::New(info.Env(), "Incorrect buffer data.")
    //        .ThrowAsJavaScriptException();
    //    return Value();
    //  }

    //  InitData(data, testLength);
    //  return buffer;
    //}

    //Value CreateExternalBufferWithFinalizeHint(const CallbackInfo& info) {
    //  finalizeCount = 0;

    //  uint8_t* data = new uint8_t[testLength];

    //  char* hint = nullptr;
    //  ArrayBuffer buffer = ArrayBuffer::New(
    //      info.Env(),
    //      data,
    //      testLength,
    //      [](Env /*env*/, void* finalizeData, char* /*finalizeHint*/) {
    //        delete[] static_cast<uint8_t*>(finalizeData);
    //        finalizeCount++;
    //      },
    //      hint);

    //  if (buffer.ByteLength() != testLength) {
    //    Error::New(info.Env(), "Incorrect buffer length.")
    //        .ThrowAsJavaScriptException();
    //    return Value();
    //  }

    //  if (buffer.Data() != data) {
    //    Error::New(info.Env(), "Incorrect buffer data.")
    //        .ThrowAsJavaScriptException();
    //    return Value();
    //  }

    //  InitData(data, testLength);
    //  return buffer;
    //}

    private static JSValue CheckBuffer(JSCallbackArgs args)
    {
        //      if (!info[0].IsArrayBuffer()) {
        //        Error::New(info.Env(), "A buffer was expected.")
        //            .ThrowAsJavaScriptException();
        //        return;
        //      }

        //ArrayBuffer buffer = info[0].As<ArrayBuffer>();

        //      if (buffer.ByteLength() != testLength) {
        //        Error::New(info.Env(), "Incorrect buffer length.")
        //            .ThrowAsJavaScriptException();
        //        return;
        //      }

        //      if (!VerifyData(static_cast<uint8_t*>(buffer.Data()), testLength))
        //{
        //    Error::New(info.Env(), "Incorrect buffer data.")
        //        .ThrowAsJavaScriptException();
        //        return JSValue.Undefined;
        //}
        return JSValue.Undefined;
    }

    private JSValue GetFinalizeCount(JSCallbackArgs _) => _finalizeCount;

    //Value CreateBufferWithConstructor(const CallbackInfo& info) {
    //  ArrayBuffer buffer = ArrayBuffer::New(info.Env(), testLength);
    //  if (buffer.ByteLength() != testLength) {
    //    Error::New(info.Env(), "Incorrect buffer length.")
    //        .ThrowAsJavaScriptException();
    //    return Value();
    //  }
    //  InitData(static_cast<uint8_t*>(buffer.Data()), testLength);
    //  ArrayBuffer buffer2(info.Env(), buffer);
    //  return buffer2;
    //}

    //Value CheckEmptyBuffer(const CallbackInfo& info) {
    //  ArrayBuffer buffer;
    //  return Boolean::New(info.Env(), buffer.IsEmpty());
    //}

    //void CheckDetachUpdatesData(const CallbackInfo& info) {
    //  if (!info[0].IsArrayBuffer()) {
    //    Error::New(info.Env(), "A buffer was expected.")
    //        .ThrowAsJavaScriptException();
    //    return;
    //  }

    //  ArrayBuffer buffer = info[0].As<ArrayBuffer>();

    //  // This potentially causes the buffer to cache its data pointer and length.
    //  buffer.Data();
    //  buffer.ByteLength();

    //#if NAPI_VERSION >= 7
    //  if (buffer.IsDetached()) {
    //    Error::New(info.Env(), "Buffer should not be detached.")
    //        .ThrowAsJavaScriptException();
    //    return;
    //  }
    //#endif

    //  if (info.Length() == 2) {
    //    // Detach externally (in JavaScript).
    //    if (!info[1].IsFunction()) {
    //      Error::New(info.Env(), "A function was expected.")
    //          .ThrowAsJavaScriptException();
    //      return;
    //    }

    //    Function detach = info[1].As<Function>();
    //    detach.Call({});
    //  } else {
    //#if NAPI_VERSION >= 7
    //    // Detach directly.
    //    buffer.Detach();
    //#else
    //    return;
    //#endif
    //  }

    //#if NAPI_VERSION >= 7
    //  if (!buffer.IsDetached()) {
    //    Error::New(info.Env(), "Buffer should be detached.")
    //        .ThrowAsJavaScriptException();
    //    return;
    //  }
    //#endif

    //  if (buffer.Data() != nullptr) {
    //    Error::New(info.Env(), "Incorrect data pointer.")
    //        .ThrowAsJavaScriptException();
    //    return;
    //  }

    //  if (buffer.ByteLength() != 0) {
    //    Error::New(info.Env(), "Incorrect buffer length.")
    //        .ThrowAsJavaScriptException();
    //    return;
    //  }
    //}

    public static JSObject Init()
    {
        var test = new TestArrayBuffer();
        return new()
        {
            Method(CreateBuffer),
            Method(test.CreateExternalBuffer),
            //Method(CreateExternalBufferWithFinalize),
            Method(CheckBuffer),
            Method(test.GetFinalizeCount),
            //Method(CheckDetachUpdatesData),
        };
    }
}
