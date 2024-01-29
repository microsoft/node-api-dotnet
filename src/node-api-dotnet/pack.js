// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const nodeMajorVersion = process.versions.node.split('.')[0];
if (nodeMajorVersion < 16) {
  console.error('Node.js version 16 or later is required.');
  process.exit(1);
}

const configuration = ['Debug', 'Release'].find(
  (c) => c.toLowerCase() == (process.argv[2] ?? '').toLowerCase());
const rids = process.argv.slice(3);

if (!configuration || rids.length === 0) {
  console.error('Usage: node pack.js Debug|Release rids...');
  process.exit(1);
}

const assemblyName = 'Microsoft.JavaScript.NodeApi';

const targetFrameworks = ['net8.0', 'net6.0'];
if (process.platform === 'win32') targetFrameworks.push('net472');

const aotTargetFramework = 'net8.0';

const fs = require('fs');
const path = require('path');
const childProcess = require('child_process');

const outBinDir = path.resolve(__dirname, `../../out/bin/${configuration}`);
const outPkgDir = path.resolve(__dirname, '../../out/pkg');

if (!fs.existsSync(outPkgDir)) fs.mkdirSync(outPkgDir);

packMainPackage();
console.log();
packGeneratorPackage();

function packMainPackage() {
  const packageJson = require('./package.json');
  const packageName = packageJson.name;

  // Create/clean the package staging directory under out/pkg/package-name.
  const packageStageDir = path.join(outPkgDir, packageName);
  mkdirClean(packageStageDir);

  // Write package.json with the current build version.
  const buildVersion = writePackageJson(packageStageDir, packageJson);

  // Copy script files to the staging dir.
  copyScriptFiles(packageStageDir, '.', 'init.js', 'index.d.ts');
  copyScriptFiles(packageStageDir, '../..', 'README.md');

  generateTargetFrameworkScriptFiles(packageStageDir);

  // Copy binaries to the staging dir.

  // Most binaries are platform-independent but framework-specific.
  copyFrameworkSpecificBinaries(
    targetFrameworks,
    packageStageDir,
    `NodeApi/${assemblyName}.runtimeconfig.json`,
    `NodeApi/${assemblyName}.dll`,
    `NodeApi.DotNetHost/${assemblyName}.DotNetHost.dll`,
    `NodeApi/System.Memory.dll`,
    `NodeApi/System.Runtime.CompilerServices.Unsafe.dll`,
    `NodeApi/System.Threading.Tasks.Extensions.dll`,
  );

  // The .node binary is platform-specific but framework-independent.
  copyPlatformSpecificBinaries(
    rids,
    packageStageDir,
    `NodeApi/${assemblyName}.node`,
  );

  // npm pack
  const command = `npm pack --pack-destination "${outPkgDir}"`;
  childProcess.execSync(command, { cwd: packageStageDir, stdio: 'inherit' });
  const packageFilePath = path.join(outPkgDir, `${packageName}-${buildVersion}.tgz`)
  console.log(`Successfully created package '${packageFilePath}'`);
}

function packGeneratorPackage() {
  const packageJson = require('./generator/package.json');
  const packageName = packageJson.name;

  // Create/clean the package staging directory under out/pkg/package-name.
  const packageStageDir = path.join(outPkgDir, packageName);
  mkdirClean(packageStageDir);

  // Create a node_modules link so the dependency can be resolved when linked for development.
  const dependencyPath = path.join(outPkgDir, 'node-api-dotnet');
  const linkPath = path.join(packageStageDir, 'node_modules', 'node-api-dotnet');
  if (!fs.existsSync(path.dirname(linkPath))) fs.mkdirSync(path.dirname(linkPath));
  fs.symlinkSync(dependencyPath, linkPath, process.platform === 'win32' ? 'junction' : 'dir');

  const buildVersion = writePackageJson(packageStageDir, packageJson);

  copyScriptFiles(packageStageDir, 'generator', 'index.js');
  copyScriptFiles(packageStageDir, '../..', 'README.md');

  copyFrameworkSpecificBinaries(
    [ 'net6.0' ],
    packageStageDir,
    `NodeApi.Generator/${assemblyName}.Generator.dll`,
    `NodeApi.Generator/Microsoft.CodeAnalysis.dll`,
    `NodeApi.Generator/System.Reflection.MetadataLoadContext.dll`
  );

  // npm pack
  const command = `npm pack --pack-destination "${outPkgDir}"`;
  childProcess.execSync(command, { cwd: packageStageDir, stdio: 'inherit' });
  const packageFilePath = path.join(outPkgDir, `${packageName}-${buildVersion}.tgz`)
  console.log(`Successfully created package '${packageFilePath}'`);
}

function mkdirClean(dir) {
  if (fs.existsSync(dir)) fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir);
}

function writePackageJson(packageStageDir, packageJson) {
  const buildVersion = getAssemblyFileVersion(assemblyName, aotTargetFramework, rids[0]);
  packageJson.version = buildVersion;
  if (packageJson.dependencies && packageJson.dependencies['node-api-dotnet']) {
    packageJson.dependencies['node-api-dotnet'] = buildVersion;
  }
  delete packageJson.scripts;
  const stagedPackageJsonPath = path.join(packageStageDir, 'package.json');
  fs.writeFileSync(stagedPackageJsonPath, JSON.stringify(packageJson, undefined, '  '));
  console.log(stagedPackageJsonPath);
  return buildVersion;
}

function copyScriptFiles(packageStageDir, srcDir, ...scriptFiles) {
  scriptFiles.forEach(scriptFile => {
    copyFile(path.join(__dirname, srcDir, scriptFile), path.join(packageStageDir, scriptFile));
  });
}

function copyFrameworkSpecificBinaries(targetFrameworks, packageStageDir, ...binFiles) {
  targetFrameworks.forEach((tfm) => {
    const tfmStageDir = path.join(packageStageDir, tfm);
    fs.mkdirSync(tfmStageDir);
    binFiles.forEach((binFile) => {
      const [projectName, binFileName] = binFile.split('/', 2);

      // "System." assemblies like System.Memory are only needed for .NET 4.x
      if (
        tfm.includes('.') &&
        binFileName.startsWith('System.') &&
        !binFileName.includes('MetadataLoadContext')
      ) return;

      const binPath = path.join(outBinDir, projectName, tfm, rids[0], binFileName);
      copyFile(binPath, path.join(tfmStageDir, binFileName));
    });
  });
}

function copyPlatformSpecificBinaries(rids, packageStageDir, ...binFiles) {
  rids.forEach((rid) => {
    const ridStageDir = path.join(packageStageDir, rid);
    fs.mkdirSync(ridStageDir);
    binFiles.forEach((binFile) => {
      const [projectName, binFileName] = binFile.split('/', 2);
      const binPath = path.join(outBinDir, projectName, 'aot', rid, 'publish', binFileName);
      copyFile(binPath, path.join(ridStageDir, binFileName));
    });
  });
}

function getAssemblyFileVersion(assemblyName, targetFramework, rid) {
  const projectName = assemblyName.split('.').slice(2).join('.');
  const depsFilePath = path.join(
    outBinDir, projectName, targetFramework, rid, assemblyName + '.deps.json');
  const depsJson = require(depsFilePath);
  const assemblyVersion = Object.keys(depsJson.libraries)
    .filter((k) => k.startsWith(assemblyName + '/'))[0].substring(assemblyName.length + 1);
  return assemblyVersion;
}

function copyFile(sourceFilePath, destFilePath) {
  console.log(`${sourceFilePath} -> ${destFilePath}`);
  fs.copyFileSync(sourceFilePath, destFilePath);
}

function generateTargetFrameworkScriptFiles(packageStageDir) {
  // Generate `index.js` for the default target framework, plus one for each supported target.
  generateTargetFrameworkScriptFile(path.join(packageStageDir, 'index.js'));
  for (let tfm of targetFrameworks) {
    generateTargetFrameworkScriptFile(path.join(packageStageDir, tfm + '.js'), tfm);
  }
}

function generateTargetFrameworkScriptFile(filePath, tfm) {
  // Each generated entrypoint script uses `init.js` to request a specific target framework version.
  const js = `const initialize = require('./init');
module.exports = initialize(${tfm ? `'${tfm}'` : ''});
`;
  fs.writeFileSync(filePath, js);

  // Also generate a `.d.ts` file for each tfm, which just re-exports the default index.
  if (tfm) {
    const dts = `import './index';
export * from 'node-api-dotnet';
`;
    fs.writeFileSync(filePath.replace(/\.js$/, '.d.ts'), dts);
  }
}
