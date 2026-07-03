#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const desktopRoot = resolve(scriptDirectory, '..');
const repoRoot = resolve(desktopRoot, '../..');
const webRoot = join(repoRoot, 'apps/web');
const manifestPath = join(desktopRoot, 'desktop.manifest.json');
const mode = process.argv[2];

if (!['build', 'dev'].includes(mode)) {
  throw new Error('Usage: node scripts/run-web.mjs <build|dev>');
}

const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const env = {
  ...process.env,
  VITE_DEFAULT_CLOUD_API_BASE_URL: process.env.VITE_DEFAULT_CLOUD_API_BASE_URL ??
    manifest.defaultCloudApiBaseUrl ??
    '',
};
const result = process.platform === 'win32'
  ? spawnSync(`${npmCommand} run ${mode}`, {
      cwd: webRoot,
      env,
      shell: true,
      stdio: 'inherit',
    })
  : spawnSync(npmCommand, ['run', mode], {
      cwd: webRoot,
      env,
      stdio: 'inherit',
    });

if (result.error) {
  throw result.error;
}

if (result.status !== 0) {
  throw new Error(`apps/web npm run ${mode} failed with exit code ${result.status}.`);
}
