// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const fs = require('node:fs');
const path = require('node:path');
const typedoc = require('typedoc');

const srcDir = path.resolve(__dirname, '../../src/node-api-dotnet');
const outDir = path.resolve(__dirname, '../reference/js');

const typedefsFile = path.join(srcDir, 'index.d.ts');
const jsonFile = path.join(outDir, 'api.json');
const markdownFile = path.join(outDir, 'index.md');

console.log('Creating directory: ' + outDir);
if (fs.existsSync(outDir)) fs.rmSync(outDir, { recursive: true, force: true });
fs.mkdirSync(outDir, { recursive: true });

exportJsdocToJson(typedefsFile, jsonFile)
  .then(() => convertJsonToMarkdown(jsonFile, markdownFile))
  .catch ((e) => {
    console.error(e.message || e);
    process.exit(1);
  });

async function exportJsdocToJson(typedefsFile, jsonFile) {
  if (!fs.existsSync(typedefsFile)) {
    throw new Error(`File not found: ${typedefsFile}`);
  }

  console.log('Exporting JSDoc from ' + typedefsFile);

  const app = await typedoc.Application.bootstrap({
    entryPoints: [typedefsFile],
    tsconfig: path.join(path.dirname(typedefsFile), 'tsconfig.json'),
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

  await app.generateJson(project, jsonFile);
}

function convertJsonToMarkdown(jsonFile, markdownFile) {
  console.log('Generating markdown from ' + jsonFile);

  /** @type {typedoc.Models.ProjectReflection} */
  const project = JSON.parse(fs.readFileSync(jsonFile, 'utf8'));

  let markdown = `---
editLink: false
outline: deep
---
`;

  markdown += `# ${project.name} package\n`;

  if (project.comment?.summary) {
    markdown += '\n' + commentToMarkdown(project.comment.summary) + '\n';
  }

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

  fs.writeFileSync(markdownFile, markdown);
  console.log('Generated ' + markdownFile);
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
