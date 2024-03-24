// This module wraps the ES import keyword in a CommonJS function,
// to enable directly importing ES modules into .NET.
module.exports = function importModule(modulePath) { return import(modulePath); };
