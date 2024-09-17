import { defineConfig } from 'vitepress'
import dotnetApiNavTree from '../reference/dotnet/nav.mjs'

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Node API for .NET",
  description: "Advanced interoperability between .NET and JavaScript in the same process",
  lang: 'en-US',
  base: '/node-api-dotnet/',

  metaChunk: true,

  themeConfig: {
    // https://vitepress.dev/reference/default-theme-config

    logo: '/images/node-api-dotnet-logo.svg',

    sidebar: [
      { text: 'Overview', link: '/overview' },
      {
        text: 'Get Started',
        items: [
          {
            text: 'JS / .NET interop scenarios',
            link: '/scenarios/index',
            collapsed: false,
            items: [
              { text: 'Dynamic .NET from JS', link: '/scenarios/js-dotnet-dynamic' },
              { text: '.NET module for Node.js', link: '/scenarios/js-dotnet-module' },
              { text: '.NET Native AOT for Node.js', link: '/scenarios/js-aot-module' },
              { text: 'Embedding JS in .NET', link: '/scenarios/dotnet-js' },
            ],
          },
          { 'text': 'Requirements', link: '/requirements' },
          { 'text': 'Example projects', link: '/examples' },
        ]
      },
      {
        text: 'Features',
        items: [
          { text: 'Type definitions', link: '/features/type-definitions' },
          { text: 'JS / .NET Marshalling', link: '/features/js-dotnet-marshalling' },
          { text: 'JS types in .NET', link: '/features/js-types-in-dotnet' },
          { text: 'JS value scopes', link: '/features/js-value-scopes' },
          { text: 'JS references', link: '/features/js-references' },
          { text: 'JS threading & async', link: '/features/js-threading-async' },
          { text: 'Node worker threads', link: '/features/node-workers' },
          { text: '.NET Native AOT', link: '/features/dotnet-native-aot' },
          { text: 'Performance', link: '/features/performance' },
        ]
      },
      {
        text: 'Reference',
        items: [
          {
            text: 'JS / .NET type mappings',
            link: '/reference/js-dotnet-types',
            collapsed: true,
            items: [
              { text: 'Basic types', link: '/reference/basic-types' },
              { text: 'Null & undefined', link: '/reference/null-undefined' },
              { text: 'Classes & interfaces', link: '/reference/classes-interfaces' },
              { text: 'Structs & tuples', link: '/reference/structs-tuples' },
              { text: 'Enums', link: '/reference/enums' },
              { text: 'Arrays & collections', link: '/reference/arrays-collections' },
              { text: 'Delegates', link: '/reference/delegates' },
              { text: 'Streams', link: '/reference/streams' },
              { text: 'Dates & times', link: '/reference/dates' },
              { text: 'Other special types', link: '/reference/other-types' },
              { text: 'Async & promises', link: '/reference/async-promises' },
              { text: 'Ref & out parameters', link: '/reference/ref-out-params' },
              { text: 'Generics', link: '/reference/generics' },
              { text: 'Extension methods', link: '/reference/extension-methods' },
              { text: 'Overloaded methods', link: '/reference/overloaded-methods' },
              { text: 'Events', link: '/reference/events' },
              { text: 'Exceptions', link: '/reference/exceptions' },
              { text: 'Namespaces', link: '/reference/namespaces' },
            ],
          },
          { text: 'MSBuild properties', link: '/reference/msbuild-props' },
          { text: 'Packages & releases', link: '/reference/packages-releases' },
        ]
      },

      // API docs might belong under "Reference", but the vitepress sidebar has a max depth of 6,
      // which .NET API docs would exceed if they were one level deeper.
      {
        text: 'JavaScript APIs',
        items: [
          {
            text: 'node-api-dotnet',
            link: '/reference/js/',
          }
        ]
      },
      {
        text: '.NET APIs',
        link: '/reference/dotnet/',
        items: dotnetApiNavTree,
      },

      { text: 'Support', link: '/support' },
      { text: 'Contributing', link: '/contributing' },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/microsoft/node-api-dotnet' },
    ],

    search: {
      provider: 'local',
    },

    editLink: {
        pattern: 'https://github.com/microsoft/node-api-dotnet/docs/:path',
    },

    footer: {
      message: 'Released under the MIT license',
      copyright: 'Copyright © 2023-present Microsoft',
    }
  }
})
