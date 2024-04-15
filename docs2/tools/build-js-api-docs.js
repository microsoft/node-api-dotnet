// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const fs = require('node:fs');
const path = require('node:path');
const typedoc = require('typedoc');

const srcDir = path.resolve(__dirname, '../../src/node-api-dotnet');
const outDir = path.resolve(__dirname, '../reference/js');

exportJsdocToJson()
  .then(convertJsonToMarkdown)
  .then((md) => fs.writeFileSync(path.join(outDir, 'index.md'), md))
  .catch ((e) => {
    console.error(e.message || e);
    process.exit(1);
  });

async function exportJsdocToJson() {
  if (fs.existsSync(outDir)) fs.rmSync(outDir, { recursive: true, force: true });
  fs.mkdirSync(outDir, { recursive: true });

  const packageTypedefsFile = path.join(srcDir, 'index.d.ts');
  if (!fs.existsSync(packageTypedefsFile)) {
    throw new Error(`File not found: ${packageTypedefsFile}`);
  }

  const app = await typedoc.Application.bootstrap({
    entryPoints: [packageTypedefsFile],
    tsconfig: path.join(path.dirname(packageTypedefsFile), 'tsconfig.json'),
    exclude: '**/node_modules/**',
    excludeExternals: true,
    excludePrivate: true,
    excludeProtected: true,
    excludeInternal: true,
    readme: 'none',
  });
  app.options.addReader(new typedoc.TSConfigReader());
  const project = await app.convert();
  if (!project) {
    throw new Error('Failed to convert TypeScript to documentation.');
  }

  const jsonFile = path.join(outDir, 'api.json');
  await app.generateJson(project, jsonFile);
  const json = fs.readFileSync(jsonFile, 'utf8');
  return json;
}

function convertJsonToMarkdown(json) {
  /** @type {typedoc.Models.ProjectReflection} */
  const project = JSON.parse(json);

  let markdown = `# ${project.name} package\n`;

  markdown += `
  ::: code-group
  \`\`\`JavaScript [ES (TS or JS)]
  import dotnet from '${project.name}';
  \`\`\`
  \`\`\`TypeScript [CommonJS (TS)]
  import * as dotnet from '${project.name}';
  \`\`\`
  \`\`\`JavaScript [CommonJS (JS)]
  const dotnet = require('${project.name}');
  \`\`\`
  :::
`;
  markdown += 'To load a specific version of .NET, append the target framework moniker to ' +
    'the package name:\n';
  markdown += `
  ::: code-group
  \`\`\`JavaScript [ES (TS or JS)]
  import dotnet from '${project.name}/net6.0';
  \`\`\`
  \`\`\`TypeScript [CommonJS (TS)]
  import * as dotnet from '${project.name}/net6.0';
  \`\`\`
  \`\`\`JavaScript [CommonJS (JS)]
  const dotnet = require('${project.name}/net6.0');
  \`\`\`
  :::
`;
  markdown += 'Currently the supported target frameworks are `net472`, `net6.0`, and `net8.0`.';

  const propertyReflections = project.children
    .filter((item) => item.kind === typedoc.Models.ReflectionKind.Variable);
  if (propertyReflections.length > 0) {
    markdown += '\n## Properties\n';
    for (const propertyReflection of propertyReflections) {
      markdown += convertPropertyReflectionToMarkdown(propertyReflection);
    }
  }

  const functionReflections = project.children
    .filter((item) => item.kind === typedoc.Models.ReflectionKind.Function);
  if (functionReflections.length > 0) {
    markdown += '\n## Methods\n';
    for (const functionReflection of functionReflections) {
      markdown += convertFunctionReflectionToMarkdown(functionReflection);
    }
  }

  return markdown;
}

function convertPropertyReflectionToMarkdown(
  /** @type {typedoc.Models.ProjectReflection} */
  item,
) {
  let markdown = `\n### ${item.name} property\n`;
  markdown += '```TypeScript\n';
  markdown += `const dotnet.${item.name}: ${typeToMarkdown(item.type)}\n`;
  markdown += '```\n';
  markdown += commentToMarkdown(item.comment?.summary) + '\n';
  return markdown;
}

function convertFunctionReflectionToMarkdown(
  /** @type {typedoc.Models.ProjectReflection} */
  item,
) {
  let markdown = `\n### ${item.name} method\n`;
  for (let signature of item.signatures) {
    let parameters = signature.parameters.map(
      (param) => `${param.name}: ${typeToMarkdown(param.type)}`);
    if (parameters.length > 1) {
      parameters = parameters.map((p) => '\n    ' + p + ',');
      parameters[parameters.length - 1] += '\n';
    }
    markdown += '```TypeScript\n';
    markdown += `dotnet.${item.name}(${parameters.join('')}): ${signature.type?.name}\n`;
    markdown += '```\n';
    markdown += commentToMarkdown(signature.comment?.summary) + '\n';

    for (let param of signature.parameters.filter((p) => p.comment?.summary)) {
      markdown += `- **${param.name}**: ${commentToMarkdown(param.comment?.summary)}\n`;
    }
    if (signature.comment?.blockTags?.length > 0) {
      const returnsTag = signature.comment.blockTags.find((t) => t.tag === '@returns');
      if (returnsTag) {
        markdown += `- Returns: ${commentToMarkdown(returnsTag.content)}\n`;
      }

      const descriptionTag = signature.comment.blockTags.find((t) => t.tag === '@description');
      if (descriptionTag) {
        markdown += '\n' + commentToMarkdown(descriptionTag.content) + '\n';
      }
    }
  }
  return markdown;
}

function typeToMarkdown(
  /** @type {typedoc.Models.SomeType} */
  type,
) {
  if (type?.type === 'literal') {
    return JSON.stringify(type.value);
  } else if (type?.name) {
    return type.name;
  } else if (type.declaration?.signatures?.length === 1) {
    const signature = type.declaration.signatures[0];
    const parameters = signature.parameters.map(
      (param) => `${param.name}: ${typeToMarkdown(param.type)}`);
    return `(${parameters.join(', ')}) => ${typeToMarkdown(signature.type)}`;
  } else {
    return 'unknown';
  }
}

function commentToMarkdown(
  /** @type {typedoc.Models.CommentDisplayPart[]} */
  comment,
) {
  return (comment || []).map((part) => part.text).join('');
}
