const assert = require('assert');
const fs = require('fs/promises');
const { Duplex, Readable, Writable } = require('stream');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const Streams = binding.Streams;
const TestStream = binding.TestStream;

const testFile = 'streams-test.txt';
const testLineCount = 10000;
testStreams();

async function testStreams() {
  await writeToDotNetStreamAsync();
  await readFromDotNetStreamAsync();
  await fs.unlink(testFile);
  await readFromNodeStreamAsync();
  await writeToNodeStreamAsync();
  await readFromTestStreamAsync();
  await writeToTestStreamAsync();
}

/**
 * Gets a Node.js Writable adapter for a .NET stream to the test file, and writes lines to the file.
 */
async function writeToDotNetStreamAsync() {
  const writableStream = Streams.writeFile(testFile);
  assert(writableStream instanceof Writable);

  writableStream.on('error', (err) => {
    console.error(err);
  });

  for (let i = 1; i <= testLineCount; i++) {
    await writeAsync(writableStream, i + '\n');
  }
  await endAsync(writableStream);
  writableStream.destroy();
}

function writeAsync(stream, data) {
  return new Promise((resolve, reject) => {
    const cb = (err) => err ? reject(err) : resolve();
    if (!stream.write(data)) {
      stream.once('drain', cb);
    } else {
      process.nextTick(cb);
    }
  });
}

function endAsync(stream) {
  return new Promise((resolve, reject) => {
    const cb = (err) => err ? reject(err) : resolve();
    stream.end(cb);
  });
}

/**
 * Gets a Node.js Readable adapter for a .NET stream to the test file, reads lines from the file,
 * and validates the expected content.
 */
async function readFromDotNetStreamAsync() {
  const readableStream = Streams.readFile(testFile);
  assert(readableStream instanceof Readable);

  await new Promise((resolve, reject) => {
    let allData = Buffer.alloc(0);
    readableStream.on('error', (err) => {
      console.error(err);
    });
    readableStream.on('data', (chunk) => {
      allData = Buffer.concat([allData, chunk]);
    });
    readableStream.on('end', () => {
      try {
        const lines = allData.toString().trimEnd().split('\n');
        for (let i = 1; i <= testLineCount; i++) {
          assert.equal(lines[i - 1], i.toString());
        }
      } catch (e) { reject(e); }
      resolve();
    });
  });
  readableStream.destroy();
}

/**
 * Creates a Node.js Readable stream and passes it to .NET, which reads from the stream
 * using a .NET stream adapter, then pipes the data to the test file.
 */
async function readFromNodeStreamAsync() {
  const data = Buffer.from(
    [...Array(testLineCount).keys()].map((i) => i + 1).join('\n') + '\n');
  const readableStream = new Readable({
    read() {
      this.push(data);
      this.push(null);
    },
  });

  assert(!readableStream.readableObjectMode);
  await Streams.pipeToFileAsync(testFile, readableStream);
}

/**
 * Creates a Node.js Writable stream that writes to a buffer, then passes the stream to .NET,
 * which pipes data from the test file to the stream using a .NET stream adapter. Then
 * validates the resulting buffer.
 */
async function writeToNodeStreamAsync() {
  let allData = Buffer.alloc(0);
  const writableStream = new Writable({
    write(chunk, encoding, cb) {
      allData = Buffer.concat([allData, chunk]);
      cb();
    },
  });

  await Streams.pipeFromFileAsync(testFile, writableStream);

  const lines = allData.toString().trimEnd().split('\n');
  for (let i = 1; i <= testLineCount; i++) {
    assert.equal(lines[i - 1], i.toString());
  }
}

/**
 * Creates an instance of a .NET Stream subclass and reads from it using a JS adapter.
 */
async function readFromTestStreamAsync()
{
  const data =
    [...Array(testLineCount).keys()].map((i) => i + 1).join('\n') + '\n';

  const readableStream = new TestStream(data);
  assert(readableStream instanceof Readable);

  await new Promise((resolve, reject) => {
    let allData = Buffer.alloc(0);
    readableStream.on('error', (err) => {
      console.error(err);
    });
    readableStream.on('data', (chunk) => {
      allData = Buffer.concat([allData, chunk]);
    });
    readableStream.on('end', () => {
      try {
        const lines = allData.toString().trimEnd().split('\n');
        for (let i = 1; i <= testLineCount; i++) {
          assert.equal(lines[i - 1], i.toString());
        }
      } catch (e) { reject(e); }
      resolve();
    });
  });
  readableStream.destroy();
}

/**
 * Creates an instance of a .NET Stream subclass and writes to it using a JS adapter.
 */
async function writeToTestStreamAsync()
{
  const writableStream = new TestStream(100000);
  assert(writableStream instanceof Writable);

  writableStream.on('error', (err) => {
    console.error(err);
  });

  for (let i = 1; i <= testLineCount; i++) {
    await writeAsync(writableStream, i + '\n');
  }
  await endAsync(writableStream);

  const allData = TestStream.getData(writableStream);
  const lines = allData.trimEnd().split('\n');
  for (let i = 1; i <= testLineCount; i++) {
    assert.equal(lines[i - 1], i.toString());
  }

  writableStream.destroy();
}
