// Ported from https://github.com/agracio/edge-js/blob/master/performance/marshal_clr2v8.js

// If using the test runner, the measurement results can be found in:
// out/obj\$(Configuration)\TestCases\edgejs-perf\hosted-measure-latency.log
// out/obj\$(Configuration)\TestCases\edgejs-perf\aot-measure-latency.log

/** @type {import('./edgejs-perf')} */
const binding = require('../common').binding;

const callCount = process.env.EDGE_CALL_COUNT || 10000;

const measure = function (func) {
	var start = Date.now();
	var i = 0;

	function one() {
		func({
			title: 'Run .NET and node.js in-process with edge.js',
			author: {
				first: 'Anonymous',
				last: 'Author'
			},
			year: 2013,
			price: 24.99,
			available: true,
			description: 'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus posuere tincidunt felis, et mattis mauris ultrices quis. Cras molestie, quam varius tincidunt tincidunt, mi magna imperdiet lacus, quis elementum ante nibh quis orci. In posuere erat sed tellus lacinia luctus. Praesent sodales tellus mauris, et egestas justo. In blandit, metus non congue adipiscing, est orci luctus odio, non sagittis erat orci ac sapien. Proin ut est id enim mattis volutpat. Vivamus ultrices dapibus feugiat. In dictum tincidunt eros, non pretium nisi rhoncus in. Duis a lacus et elit feugiat ullamcorper. Mauris tempor turpis nulla. Nullam nec facilisis elit.',
			picture: Buffer.alloc(16000),
			tags: [ '.NET', 'node.js', 'CLR', 'V8', 'interop']
		}, function (error, callbck) {
			if (error) throw error;
			if (++i < callCount) setImmediate(one);
			else finish();
		});
	}

	function finish() {
		var delta = Date.now() - start;
		var result = process.memoryUsage();
		result.latency = delta / callCount;
		console.log(result);
	}

	one();
};

const invoke = binding.Startup.invoke;
measure((input, callback) => {
  const result = invoke(input);
  callback(null, result);
});
