#!/usr/bin/env node
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDirectory, '..');
const options = parseArgs(process.argv.slice(2));
const targetPath = resolveTargetPath(options.target);
const target = readJson(targetPath);

validateTarget(target, targetPath);

const generatedTargetPath = join(
  repoRoot,
  'apps/web/src/generated/deploymentTarget.ts',
);
const packagePath = join(repoRoot, 'apps/desktop/package.json');
const packageLockPath = join(repoRoot, 'apps/desktop/package-lock.json');
const tauriConfigPath = join(
  repoRoot,
  'apps/desktop/src-tauri/tauri.conf.json',
);
const cargoTomlPath = join(repoRoot, 'apps/desktop/src-tauri/Cargo.toml');

const plannedWrites = [
  updateTextFile(generatedTargetPath, () => renderGeneratedTarget(target)),
  updateJsonFile(packagePath, (packageJson) => {
    packageJson.version = target.version;
  }),
  updateJsonFile(packageLockPath, (packageLock) => {
    packageLock.version = target.version;

    if (packageLock.packages?.['']) {
      packageLock.packages[''].version = target.version;
    }
  }),
  updateJsonFile(tauriConfigPath, (tauriConfig) => {
    tauriConfig.productName = target.productName;
    tauriConfig.version = target.version;
    tauriConfig.identifier = target.identifier;
    tauriConfig.build ??= {};
    tauriConfig.build.beforeDevCommand = 'node scripts/run-web.mjs dev';
    tauriConfig.build.beforeBuildCommand = 'node scripts/run-web.mjs build';
    tauriConfig.app ??= {};
    tauriConfig.app.windows ??= [];

    for (const windowConfig of tauriConfig.app.windows) {
      windowConfig.title = target.windowTitle;
    }

    tauriConfig.bundle ??= {};
    tauriConfig.bundle.publisher = target.publisher;
  }),
  updateTextFile(cargoTomlPath, (cargoToml) => {
    let updated = replaceTomlValue(cargoToml, 'version', target.version);
    updated = replaceTomlValue(updated, 'description', target.description);
    return updated;
  }),
].filter(Boolean);

if (options.check) {
  if (plannedWrites.length > 0) {
    console.error(
      `Client configuration is not synced with ${relativeToRepo(targetPath)}:`,
    );

    for (const write of plannedWrites) {
      console.error(`- ${relativeToRepo(write.path)}`);
    }

    process.exit(1);
  }

  console.log(
    `Client configuration matches ${relativeToRepo(targetPath)}.`,
  );
  process.exit(0);
}

for (const write of plannedWrites) {
  writeFileSync(write.path, write.next, 'utf8');
  console.log(`Updated ${relativeToRepo(write.path)}`);
}

if (plannedWrites.length === 0) {
  console.log('Client configuration is already synced.');
}

function parseArgs(args) {
  const parsed = {
    check: false,
    target: process.env.DUMPTETHER_DEPLOYMENT_TARGET ?? 'standalone',
  };

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index];

    if (arg === '--check') {
      parsed.check = true;
      continue;
    }

    if (arg === '--target') {
      parsed.target = args[index + 1];
      index += 1;

      if (!parsed.target) {
        throw new Error('--target requires a target name or JSON path.');
      }

      continue;
    }

    throw new Error(`Unknown argument '${arg}'.`);
  }

  return parsed;
}

function resolveTargetPath(targetNameOrPath) {
  if (isAbsolute(targetNameOrPath)) {
    return targetNameOrPath;
  }

  const candidatePath = resolve(repoRoot, targetNameOrPath);
  if (candidatePath.endsWith('.json')) {
    return candidatePath;
  }

  return join(repoRoot, 'deploy/targets', `${targetNameOrPath}.json`);
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
  let current = '';

  try {
    current = readFileSync(path, 'utf8');
  } catch (error) {
    if (error?.code !== 'ENOENT') {
      throw error;
    }
  }

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

function renderGeneratedTarget(candidate) {
  const publicTarget = {
    targetId: candidate.targetId,
    displayName: candidate.productName,
    webOrigin: candidate.webOrigin,
    cloudApiBaseUrl: candidate.cloudApiBaseUrl,
    updateChannel: candidate.updateChannel,
  };

  return `// Generated by scripts/configure-client.mjs. Do not edit directly.
export const deploymentTarget = ${JSON.stringify(publicTarget, null, 2)} as const;
`;
}

function validateTarget(candidate, path) {
  if (candidate.schemaVersion !== 1) {
    throw new Error(`${relativeToRepo(path)} must use schemaVersion 1.`);
  }

  const requiredStringKeys = [
    'targetId',
    'productName',
    'version',
    'publisher',
    'identifier',
    'windowTitle',
    'description',
    'webOrigin',
    'cloudApiBaseUrl',
    'updateChannel',
  ];

  for (const key of requiredStringKeys) {
    if (typeof candidate[key] !== 'string') {
      throw new Error(`${relativeToRepo(path)} must define string '${key}'.`);
    }
  }

  for (const key of requiredStringKeys.filter(
    (key) => !['webOrigin', 'cloudApiBaseUrl'].includes(key),
  )) {
    if (candidate[key].trim().length === 0) {
      throw new Error(`${relativeToRepo(path)} must define non-empty '${key}'.`);
    }
  }

  if (!/^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/.test(candidate.version)) {
    throw new Error('Deployment target version must be SemVer-like.');
  }

  if (!/^[a-z][a-z0-9]*(?:\.[a-z][a-z0-9]*)+$/.test(candidate.identifier)) {
    throw new Error('Deployment target identifier must be reverse-DNS style.');
  }

  validateOptionalAbsoluteHttpUrl(candidate.webOrigin, 'webOrigin');
  validateOptionalAbsoluteHttpUrl(
    candidate.cloudApiBaseUrl,
    'cloudApiBaseUrl',
  );
}

function validateOptionalAbsoluteHttpUrl(value, key) {
  if (!value) {
    return;
  }

  const parsed = new URL(value);
  if (!['http:', 'https:'].includes(parsed.protocol)) {
    throw new Error(`${key} must use http or https.`);
  }

  if (parsed.pathname !== '/' || parsed.search || parsed.hash) {
    throw new Error(`${key} must be an origin/base URL without a path.`);
  }
}

function relativeToRepo(path) {
  return relative(repoRoot, path).replaceAll('\\', '/');
}

function textMatches(left, right) {
  return normalizeLineEndings(left) === normalizeLineEndings(right);
}

function normalizeLineEndings(value) {
  return value.replaceAll('\r\n', '\n');
}
