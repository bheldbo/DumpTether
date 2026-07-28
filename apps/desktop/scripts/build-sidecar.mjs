#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync } from 'node:fs';
import { arch, homedir, platform } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const supportedRuntimes = new Set(['win-x64', 'win-arm64', 'linux-x64', 'linux-arm64']);
const targetTriples = new Map([
  ['win-x64', 'x86_64-pc-windows-msvc'],
  ['win-arm64', 'aarch64-pc-windows-msvc'],
  ['linux-x64', 'x86_64-unknown-linux-gnu'],
  ['linux-arm64', 'aarch64-unknown-linux-gnu'],
]);

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDirectory, '../../..');
const apiProject = join(repoRoot, 'src/DumpTether.Api/DumpTether.Api.csproj');
const binaryRoot = join(repoRoot, 'apps/desktop/src-tauri/binaries');

const options = parseArgs(process.argv.slice(2));
const runtime = resolveRuntime(options.runtime);
const targetTriple = targetTriples.get(runtime);
const extension = runtime.startsWith('win-') ? '.exe' : '';
const publishDirectory = join(binaryRoot, 'publish', runtime);
const publishedBinary = join(publishDirectory, `DumpTether.Api${extension}`);
const sidecarBinary = join(binaryRoot, `dumptether-api-${targetTriple}${extension}`);

setNuGetPackageCache();
mkdirSync(publishDirectory, { recursive: true });

const restoreArgs = ['restore', apiProject, '-r', runtime, '--disable-build-servers'];
if (options.ignoreFailedSources) {
  restoreArgs.push('--ignore-failed-sources');
}

run('dotnet', restoreArgs);
run('dotnet', [
  'publish',
  apiProject,
  '-c',
  'Release',
  '-r',
  runtime,
  '--no-restore',
  '--disable-build-servers',
  '-m:1',
  '--self-contained',
  String(options.selfContained),
  '/p:BuildInParallel=false',
  '/p:UseSharedCompilation=false',
  '/p:PublishSingleFile=true',
  '/p:IncludeNativeLibrariesForSelfExtract=true',
  '/p:PublishTrimmed=false',
  '-o',
  publishDirectory,
]);

if (!existsSync(publishedBinary)) {
  throw new Error(`Expected published API binary was not found at ${publishedBinary}.`);
}

copyFileSync(publishedBinary, sidecarBinary);
console.log(`Built DumpTether API sidecar: ${sidecarBinary}`);

function parseArgs(args) {
  const parsed = {
    ignoreFailedSources: true,
    runtime: '',
    selfContained: true,
  };

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];
    const [key, inlineValue] = arg.split('=', 2);

    switch (key) {
      case '--runtime':
        parsed.runtime = inlineValue ?? args[++index] ?? '';
        break;
      case '--self-contained':
        parsed.selfContained = parseBoolean(inlineValue ?? args[++index], key);
        break;
      case '--ignore-failed-sources':
        parsed.ignoreFailedSources = parseBoolean(inlineValue ?? args[++index], key);
        break;
      default:
        throw new Error(`Unknown argument '${arg}'.`);
    }
  }

  return parsed;
}

function parseBoolean(value, name) {
  if (value === undefined) {
    throw new Error(`${name} requires true or false.`);
  }

  if (['true', '1', 'yes'].includes(value.toLowerCase())) {
    return true;
  }

  if (['false', '0', 'no'].includes(value.toLowerCase())) {
    return false;
  }

  throw new Error(`${name} must be true or false.`);
}

function resolveRuntime(requestedRuntime) {
  if (requestedRuntime) {
    if (!supportedRuntimes.has(requestedRuntime)) {
      throw new Error('Runtime must be one of: win-x64, win-arm64, linux-x64, linux-arm64.');
    }

    return requestedRuntime;
  }

  const architecture = arch();
  const architectureSuffix = architecture === 'x64'
    ? 'x64'
    : architecture === 'arm64'
      ? 'arm64'
      : '';

  if (!architectureSuffix) {
    throw new Error(`Unsupported desktop sidecar architecture '${architecture}'.`);
  }

  if (platform() === 'win32') {
    return `win-${architectureSuffix}`;
  }

  if (platform() === 'linux') {
    return `linux-${architectureSuffix}`;
  }

  throw new Error('Unsupported desktop sidecar OS. Current supported sidecar runtimes are Windows and Linux.');
}

function run(command, args) {
  const result = spawnSync(command, args, {
    cwd: repoRoot,
    shell: platform() === 'win32',
    stdio: 'inherit',
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(' ')} failed with exit code ${result.status}.`);
  }
}

function setNuGetPackageCache() {
  if (process.env.NUGET_PACKAGES) {
    return;
  }

  const packageCache = join(homedir(), '.nuget', 'packages');
  if (existsSync(packageCache)) {
    process.env.NUGET_PACKAGES = packageCache;
  }
}
