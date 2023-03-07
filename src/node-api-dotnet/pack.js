const nodeMajorVersion = process.versions.node.split('.')[0];
if (nodeMajorVersion < 18) {
  console.error('Node.js version 18 or later is required.');
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
const targetFrameworks = ['net7.0']; // AOT binaries use the first TFM in this list.

const fs = require('fs');
const path = require('path');
const childProcess = require('child_process');

const outBinDir = path.resolve(__dirname, `../../out/bin/${configuration}`);
const outPkgDir = path.resolve(__dirname, '../../out/pkg');

const packageJson = require('./package.json');
const packageName = packageJson.name;

// Create/clean the package staging directory under out/pkg/package-name.
const packageStageDir = path.join(outPkgDir, packageName);
mkdirClean(packageStageDir);

// Write package.json with the current build version.
const buildVersion = writePackageJson();

// Copy script files to the staging dir.
copyScriptFiles('index.js', 'index.d.ts');

// Copy binaries to the staging dir.

// Most binaries are platform-independent but framework-specific.
copyFrameworkSpecificBinaries(
  `NodeApi/${assemblyName}.runtimeconfig.json`,
  `NodeApi/${assemblyName}.dll`,
  `NodeApi.DotNetHost/${assemblyName}.DotNetHost.dll`,
);

// The .node binary is platform-specific but framework-independent.
copyPlatformSpecificBinaries(
  `NodeApi/${assemblyName}.node`,
);

// npm pack
const command = `npm pack --pack-destination "${outPkgDir}"`;
childProcess.execSync(command, { cwd: packageStageDir, stdio: 'inherit' });
const packageFilePath = path.join(outPkgDir, `${packageName}-${buildVersion}.tgz`)
console.log(`Successfully created package '${packageFilePath}'`);

function mkdirClean(dir) {
  if (fs.existsSync(dir)) fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir);
}

function writePackageJson() {
  const buildVersion = getAssemblyFileVersion(assemblyName, targetFrameworks[0], rids[0]);
  packageJson.version = buildVersion;
  delete packageJson.scripts;
  const stagedPackageJsonPath = path.join(packageStageDir, 'package.json');
  fs.writeFileSync(stagedPackageJsonPath, JSON.stringify(packageJson, undefined, '  '));
  return buildVersion;
}

function copyScriptFiles(...scriptFiles) {
  scriptFiles.forEach(scriptFile => {
    copyFile(path.join(__dirname, scriptFile), path.join(packageStageDir, scriptFile));
  });
}

function copyFrameworkSpecificBinaries(...binFiles) {
  targetFrameworks.forEach((tfm) => {
    const tfmStageDir = path.join(packageStageDir, tfm);
    fs.mkdirSync(tfmStageDir);
    binFiles.forEach((binFile) => {
      const [projectName, binFileName] = binFile.split('/', 2);
      const binPath = path.join(outBinDir, projectName, tfm, rids[0], binFileName);
      copyFile(binPath, path.join(tfmStageDir, binFileName));
    });
  });
}

function copyPlatformSpecificBinaries(...binFiles) {
  rids.forEach((rid) => {
    const ridStageDir = path.join(packageStageDir, rid);
    fs.mkdirSync(ridStageDir);
    binFiles.forEach((binFile) => {
      const [projectName, binFileName] = binFile.split('/', 2);
      const tfm = targetFrameworks[0];
      const binPath = path.join(outBinDir, projectName, tfm, rid, 'publish', binFileName);
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
