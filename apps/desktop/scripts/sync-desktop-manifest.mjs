#!/usr/bin/env node
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const desktopRoot = resolve(scriptDirectory, '..');
const repoRoot = resolve(desktopRoot, '../..');
const manifestPath = join(desktopRoot, 'desktop.manifest.json');
const packagePath = join(desktopRoot, 'package.json');
const packageLockPath = join(desktopRoot, 'package-lock.json');
const tauriConfigPath = join(desktopRoot, 'src-tauri/tauri.conf.json');
const cargoTomlPath = join(desktopRoot, 'src-tauri/Cargo.toml');

const options = parseArgs(process.argv.slice(2));
const manifest = readJson(manifestPath);
validateManifest(manifest);

const plannedWrites = [
  updateJsonFile(packagePath, (packageJson) => {
    packageJson.version = manifest.version;
  }),
  updateJsonFile(packageLockPath, (packageLock) => {
    packageLock.version = manifest.version;

    if (packageLock.packages?.['']) {
      packageLock.packages[''].version = manifest.version;
    }
  }),
  updateJsonFile(tauriConfigPath, (tauriConfig) => {
    tauriConfig.productName = manifest.productName;
    tauriConfig.version = manifest.version;
    tauriConfig.identifier = manifest.identifier;
    tauriConfig.build ??= {};
    tauriConfig.build.beforeDevCommand = 'node scripts/run-web.mjs dev';
    tauriConfig.build.beforeBuildCommand = 'node scripts/run-web.mjs build';
    tauriConfig.app ??= {};
    tauriConfig.app.windows ??= [];

    for (const windowConfig of tauriConfig.app.windows) {
      windowConfig.title = manifest.windowTitle;
    }

    tauriConfig.bundle ??= {};
    tauriConfig.bundle.publisher = manifest.publisher;
  }),
  updateTextFile(cargoTomlPath, (cargoToml) => {
    let updated = replaceTomlValue(cargoToml, 'version', manifest.version);
    updated = replaceTomlValue(updated, 'description', manifest.description);
    return updated;
  }),
].filter(Boolean);

if (options.check) {
  if (plannedWrites.length > 0) {
    console.error('Desktop manifest is not synced:');

    for (const write of plannedWrites) {
      console.error(`- ${relativeToRepo(write.path)}`);
    }

    process.exit(1);
  }

  console.log('Desktop manifest is synced.');
  process.exit(0);
}

for (const write of plannedWrites) {
  writeFileSync(write.path, write.next, 'utf8');
  console.log(`Updated ${relativeToRepo(write.path)}`);
}

if (plannedWrites.length === 0) {
  console.log('Desktop manifest is already synced.');
}

function parseArgs(args) {
  const parsed = { check: false };

  for (const arg of args) {
    if (arg === '--check') {
      parsed.check = true;
      continue;
    }

    throw new Error(`Unknown argument '${arg}'.`);
  }

  return parsed;
}

function readJson(path) {
  return JSON.parse(readFileSync(path, 'utf8'));
}

function updateJsonFile(path, update) {
  const current = readFileSync(path, 'utf8');
  const parsed = JSON.parse(current);

  update(parsed);

  const next = `${JSON.stringify(parsed, null, 2)}\n`;
  return textMatches(current, next) ? null : { path, next };
}

function updateTextFile(path, update) {
  const current = readFileSync(path, 'utf8');
  const next = update(current);
  return textMatches(current, next) ? null : { path, next };
}

function replaceTomlValue(toml, key, value) {
  const escapedValue = value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
  const pattern = new RegExp(`^${key}\\s*=\\s*".*"$`, 'm');

  if (!pattern.test(toml)) {
    throw new Error(`Could not find TOML key '${key}'.`);
  }

  return toml.replace(pattern, `${key} = "${escapedValue}"`);
}

function validateManifest(candidate) {
  const requiredStringKeys = [
    'productName',
    'version',
    'identifier',
    'publisher',
    'windowTitle',
    'description',
  ];

  for (const key of requiredStringKeys) {
    if (typeof candidate[key] !== 'string' || candidate[key].trim().length === 0) {
      throw new Error(`desktop.manifest.json must define '${key}'.`);
    }
  }

  if (!/^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/.test(candidate.version)) {
    throw new Error('desktop.manifest.json version must be SemVer-like, for example 0.1.0.');
  }

  if (!/^[a-z][a-z0-9]*(?:\.[a-z][a-z0-9]*)+$/.test(candidate.identifier)) {
    throw new Error('desktop.manifest.json identifier must be reverse-DNS style, for example net.heldbo.dumptether.');
  }

  if (typeof candidate.defaultCloudApiBaseUrl !== 'string') {
    throw new Error("desktop.manifest.json must define 'defaultCloudApiBaseUrl' as a string.");
  }
}

function relativeToRepo(path) {
  return path
    .replace(repoRoot, '')
    .replace(/^[/\\]/, '')
    .replaceAll('\\', '/');
}

function textMatches(left, right) {
  return normalizeLineEndings(left) === normalizeLineEndings(right);
}

function normalizeLineEndings(value) {
  return value.replaceAll('\r\n', '\n');
}
