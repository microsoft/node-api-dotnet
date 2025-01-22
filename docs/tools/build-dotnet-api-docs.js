// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const childProcess = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const assemblyNamePrefix = 'Microsoft.JavaScript.';
const docsTargetFramework = 'net8.0';
const docsConfiguration = 'Release';
const ridPlatform =
  process.platform === 'win32' ? 'win' :
  process.platform === 'darwin' ? 'osx' :
  process.platform;
const ridArch = process.arch === 'ia32' ? 'x86' : process.arch;
const rid = `${ridPlatform}-${ridArch}`;

const rootDir = path.resolve(__dirname, '../..');
const srcDir = path.join(rootDir, 'src');
const binDir = path.join(rootDir, 'out', 'bin');
const outDir = path.resolve(__dirname, '../reference/dotnet');

const projectList = [
  { name: 'NodeApi' },
  { name: 'NodeApi.DotNetHost', references: ['NodeApi'] },
];

try {
  console.log('Creating directory: ' + outDir);
  if (fs.existsSync(outDir)) fs.rmSync(outDir, { recursive: true, force: true });
  fs.mkdirSync(outDir, { recursive: true });

  // Build markdown for each project, and merge all indexes into a combined index file.
  let indexMarkdown = '';
  for (let project of projectList) {
    indexMarkdown += buildProjectMarkdownDocs(project.name, project.references);
  }

  console.log('Generating navigation tree: ' + path.join(outDir, 'nav.mts'));
  const nav = generateNavigationTree('package', outDir);
  fs.writeFileSync(path.join(outDir, 'nav.mts'), 'export default ' + JSON.stringify(nav, null, 2));

  // Remove the assembly headers in the combined index file and insert a new top-level header.
  indexMarkdown = '# .NET API Reference\n' + indexMarkdown.replace(/^# .*$/gm, '');

  fs.writeFileSync(path.join(outDir, 'index.md'), indexMarkdown);

  console.log('Post-processing markdown files.');
  processMarkdownFiles(outDir);
} catch (e) {
  console.error(e.message || e);
  process.exit(1);
}

/** Log a command and execute it synchronously. */
function exec(cmd) {
  console.log(cmd);
  try {
    childProcess.execSync(cmd);
  } catch (e) {
    if (e.status) throw new Error(`Command executed with status: ${e.status}`);
    throw e;
  }
}

/** Run `dotnet build` with options to produce XML doc files for a project. */
function buildProjectXmldocs(projectName, assemblyName) {
  const projectDir = path.join(srcDir, projectName);
  console.log('Building project: ' + projectDir);
  exec(`dotnet build "${projectDir}" -c ${docsConfiguration} ` +
    `-f ${docsTargetFramework} -p:GenerateDocumentationFile=true -p:NoWarn=CS1591 -t:Rebuild`);
  const projectBinDir = path.join(
    binDir,
    docsConfiguration,
    projectName,
    docsTargetFramework,
    rid);
  const assemblyFilePath = path.join(projectBinDir, assemblyName + '.dll');
  if (!fs.existsSync(assemblyFilePath)) {
    throw new Error('Assembly file was not found at ' + assemblyFilePath);
  }
  const xmldocFilePath = path.join(projectBinDir, assemblyName + '.xml');
  if (!fs.existsSync(xmldocFilePath)) {
    throw new Error('XML doc file was not found at ' + xmldocFilePath);
  }
  return assemblyFilePath;
}

/** Use the XmlDocMarkdown tool to generate markdown from the XML doc files.  */
function buildProjectMarkdownDocs(projectName, projectReferences) {
  const assemblyName = assemblyNamePrefix + projectName;
  const assemblyFilePath = buildProjectXmldocs(projectName, assemblyName);
  const assemblyReferences = (projectReferences ?? []).map((p) => assemblyNamePrefix + p);

  console.log('Generating markdown: ' + assemblyName);

  // A .csproj file for the XmlDocMarkdown tool is in the same directory as this script.
  // Recommended usage is to build the tool from source: https://ejball.com/XmlDocMarkdown/#usage
  const xmlDocMdRunCommand =
    `dotnet run -c Release -f ${docsTargetFramework} --project ${__dirname} --`;

  // Ref structs are obsolete: https://turnerj.com/blog/ref-structs-are-technically-obsolete
  // So the --obsolete option is required to include them in docs.
  exec(`${xmlDocMdRunCommand} "${assemblyFilePath}" "${outDir}" --obsolete ` +
    `--source "https://github.com/microsoft/node-api-dotnet/tree/main/src/${projectName}"` +
    assemblyReferences.map((r) => ` --external "${r}"`).join(''));

  const assemblyMarkdownFile = path.join(outDir, assemblyName + '.md');
  const assemblyMarkdown = fs.readFileSync(assemblyMarkdownFile, 'utf8');

  return assemblyMarkdown;
}

/** Process all generated markdown files and apply some minor edits. */
function processMarkdownFiles(directory) {
  fs.readdirSync(directory, { recursive: true}).forEach((file) => {
    if (/\.md$/.test(file)) {
      const filePath = path.join(directory, file);

      if (/\.napi_[a-z_]+\/Handle.md$/.test(file)) {
        // Omit "Handle" properties from napi_ record structs.
        // (They are not relevant for users, and cause problems with the VitePress build.)
        fs.rmSync(filePath);
        return;
      }

      let markdown = fs.readFileSync(filePath, 'utf8');
      markdown = processMarkdown(markdown);
      fs.writeFileSync(filePath, markdown);
    }
  });
}

/** Apply some transformations on a markdown string. */
function processMarkdown(md) {
  // Escape some special chars in markdown tables because they cause problems with vitepress:
  // https://github.com/vuejs/vitepress/issues/449
  md = md
    .replace(/(?<=\|[^|]*)(?<!&[a-z]+);(?=[^|]*\|)/g, '&semi;')
    .replace(/(?<=\|[^|]*){(?=[^|]*\|)/g, '&lbrace;')
    .replace(/(?<=\|[^|]*)}(?=[^|]*\|)/g, '&rbrace;');

  // Strip [Obsolete] attributes on ref structs.
  md = md.replace(/\[Obsolete\("Types with embedded references[^"]*"\)\]\r?\n/g, '');

  // Strip "Handle" properties from napi_ record structs.
  md = md.replace(/\| \[Handle\]\(\w+\.napi_.*\|\n/, '');

  md = md.replace(/```csharp/g, '```C#');

  // Insert YAML front matter for vitepress rendering.
  md = `---
editLink: false
prev: false
next: false
outline: false
---
` + md;

  return md;
}

/** Generate navigation data that will be imported into the vitepress sidebar config. */
function generateNavigationTree(itemType, rootDir, subDir) {
  const items = [];
  const currentDir = subDir ? path.join(rootDir, subDir) : rootDir;

  fs.readdirSync(currentDir)
    .filter((f) => /\.md$/.test(f))
    .map((f) => f.substr(0, f.length - 3))
    .sort()
    .forEach((file) => {
      extractNavItemsFromMarkdown(rootDir, subDir, path.join(currentDir, file + '.md'), items);
  });

  // Look for nested types which are in the same directory as their containing type.
  if (subDir) {
    fs.readdirSync(path.dirname(currentDir))
      .filter((f) => /\.md$/.test(f))
      .map((f) => f.substr(0, f.length - 3))
      .filter((f) => f.startsWith(path.basename(currentDir) + '.'))
      .sort()
      .forEach((file) => {
        extractNavItemsFromMarkdown(
          rootDir,
          path.dirname(subDir),
          path.join(path.dirname(currentDir), file + '.md'),
          items,
          true);
    });
  }

  return categorizeNavItems(itemType, items);
}

function extractNavItemsFromMarkdown(rootDir, subDir, markdownFile, items, allowNested) {
  const referencePath = '/reference/dotnet/';
  let file = path.basename(markdownFile, '.md');

  // The top-level markdown files are per-assembly, but the nav should start with namespaces.
  let headers = getMarkdownFileHeaders(
    markdownFile,
    subDir ? '#' : '##');

  if (/ \(\d.*\)$/.test(headers[0])) {
    // Merge overloads into a single item.
    headers = [headers[0].replace(/ \(\d.*\)$/, '')];
  }

  headers.forEach((header) => {
    let link = path.join(referencePath, subDir ?? '', file).replace(/\\/g, '/');

    if (/ namespace$/.test(header)) {
      // Fix namespace links to point to subheaders in the merged index.
      link = referencePath + '#' +
          header.replace(/\.| /g, '-').toLowerCase();

      // Use the namespace name as the file (directory) name when recursing.
      file = header.replace(/ namespace$/, '');
    } else if (file.includes('.')) {
      if (!allowNested) return;
      header = header.replace(' ' + file.substr(0, file.indexOf('.')) + '.', '');
    }

    // If there's a matching subdirectory, recurse to find sub-items.
    let subItems = undefined;
    if (fs.existsSync(path.join(path.dirname(markdownFile), file)) &&
      fs.lstatSync(path.join(path.dirname(markdownFile), file)).isDirectory()
    ) {
      const itemType = header.substr(header.indexOf(' ') + 1);
      subItems = generateNavigationTree(itemType, rootDir, path.join(subDir ?? '', file));
    }

    // Shorten some header suffixes so they fit better in the sidebar.
    header = header
      .replace(/ namespace$/, '')
      .replace('structure', 'struct')
      .replace('enumeration', 'enum');

    items.push({
      text: header,
      link: link,
      items: subItems,
      collapsed: subItems ? true : undefined,
    });
  });
}

function categorizeNavItems(containerType, items) {
  const typeCategories = [
    'Interfaces',
    'Classes',
    'Structs',
    'Enums',
    'Delegates',
  ];
  const memberCategories = [
    'Constructors',
    'Properties',
    'Methods',
    'Operators',
    'Events',
    'Fields',
    'Types',
  ];
  let categories = [];
  switch (containerType) {
    case 'namespace':
      categories = typeCategories;
      break;
    case 'interface':
    case 'class':
    case 'structure':
      categories = memberCategories;
      break;
    default:
      return items;
  }

  function getCategorySuffix(category) {
    return  ' ' + (
      category === 'Properties' ? 'property' :
      category === 'Classes' ? 'class' :
      category.toLowerCase().replace(/s$/, ''));
  }

  const categorizedItems = [];

  for (let category of categories) {
    const suffixes = category == 'Types' ?
      typeCategories.map(getCategorySuffix) : [getCategorySuffix(category)];
    const categoryItems = items.filter((i) => suffixes.some((s) => i.text.endsWith(s)));
    if (categoryItems.length > 0) {
      categorizedItems.push({
        text: category,
        items: categoryItems,
        collapsed: true,
      });
    }
  }

  return categorizedItems;
}

/** Extract headers from a markdown file. */
function getMarkdownFileHeaders(file, headerPrefix) {
  const markdown = fs.readFileSync(file, 'utf8');
  const headerRegex = new RegExp(`^${headerPrefix} (.*)$`, 'gm');
  const headerMatches = markdown.matchAll(headerRegex);
  const headers = [...headerMatches].map((m) => m[1]);
  return headers;
}
