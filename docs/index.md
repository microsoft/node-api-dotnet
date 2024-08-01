---
# Markdown docs in this directory are rendered to HTML using vitepress: https://vitepress.dev/
# Browse the rendered documentation at https://microsoft.github.io/node-api-dotnet

# https://vitepress.dev/reference/default-theme-home-page

head:
  # Google Search Console site verification
  - - meta
    - name: google-site-verification
      content: WEl34DrQveQB8qaqvdfR3lybAx4ZGlaW9eOnU8KIW3Y

layout: home

hero:
  name: Node API for .NET
  tagline: Advanced interoperability between .NET and JavaScript in the same process
  image:
    src: /images/node-api-dotnet-logo.svg
    alt: Node API .NET logo
  actions:
    - theme: alt
      text: Overview
      link: /overview
    - theme: brand
      text: Get Started
      link: /scenarios/index
    - theme: alt
      text: Features
      link: /features/type-definitions
    - theme: alt
      text: JS / .NET Mappings
      link: /reference/js-dotnet-types1
    - theme: alt
      text: JS API Reference
      link: /reference/js/
    - theme: alt
      text: .NET API Reference
      link: /reference/dotnet/

features:
  - title: Call .NET from JS
    icon: 🔃
    link: /scenarios/js-dotnet-dynamic
    details: Load .NET assemblies from JavaScript and use nearly any APIs.
  - title: Call JS from .NET
    icon: 🔃
    link: /scenarios/dotnet-js
    details: Run Node.js or another JS runtime in a .NET application, with advanced interop capabilities.
  - title: Type definitions
    icon:
      dark: /images/dark/ts.svg
      light: /images/light/ts.svg
    link: /features/type-definitions
    details: Automatically generate TypeScript type definitions for .NET assemblies.
  - title: .NET Native AOT support
    icon: 🤖
    link: /features/dotnet-native-aot
    details: Optionally compile a C# library to a fully native Node.js addon that does not depend on the .NET runtime.
  - title: Automatic marshalling
    icon: 🏭
    link: /features/js-dotnet-marshalling
    details: Pass classes, collections, streams, and more seamlessly between JS and .NET.
  - title: High performance
    icon: 🚀
    link: /features/performance
    details: Build-time source-generation or runtime code-generation optimizes interop performance.
  - title: Exception propagation
    icon: 💣
    link: /reference/exceptions
    details: .NET exceptions convert to/from JS errors, with combined stack traces.
---
