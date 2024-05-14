import { CounterFactory, Counter } from "./bin/dotnet-module.js";

// No crash when constructing the counter object directly instead of via factory.
////count(new Counter());

count(CounterFactory.createCounter());

async function count(counter) {
    while (true) {
        try {
            // No crash when not using async. (Or maybe it is just less likely.)
            ////counter.incrementAsync();

            await counter.incrementAsync();

            if (counter.count % 100000 == 0) {
                console.log('count: ' + counter.count);
            }

            if (counter.count % 500000 == 0) {
                if (global.gc) global.gc();
            }
        } catch(err){
            console.log(err);
            break;
        }
    }
}
