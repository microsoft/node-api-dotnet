// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const ThreadSafety = binding.ThreadSafety;

async function test() {
    let delegateCalled = false;
    await ThreadSafety.callDelegateFromOtherThread(() => delegateCalled = true);
    assert(delegateCalled);

    let interfaceMethodArgument = false;
    await ThreadSafety.callInterfaceMethodFromOtherThread(
        {
            echo: (value) => {
                interfaceMethodArgument = value;
                return value;
            }
        },
        'test');
    assert.strictEqual(interfaceMethodArgument, 'test');

    const count = await ThreadSafety.enumerateCollectionFromOtherThread([1, 2, 3]);
    assert.strictEqual(count, 3);

    const map = new Map();
    map.set('a', '1');
    map.set('b', '2');
    map.set('c', '3');
    const mapSize = await ThreadSafety.enumerateDictionaryFromOtherThread(map);
    assert.strictEqual(mapSize, 3);

    const modifyResult = await ThreadSafety.modifyDictionaryFromOtherThread(map, 'a');
    assert(modifyResult);
    assert(!map.has('a'));
    assert.strictEqual(map.size, 2);
}
test().catch((err) => {
    console.error(err);
    process.exit(1);
});
