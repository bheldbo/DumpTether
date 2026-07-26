#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const desktopRoot = resolve(scriptDirectory, '..');
const repoRoot = resolve(desktopRoot, '../..');
const webRoot = join(repoRoot, 'apps/web');
const mode = process.argv[2];

if (!['build', 'dev'].includes(mode)) {
  throw new Error('Usage: node scripts/run-web.mjs <build|dev>');
}

const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const result = process.platform === 'win32'
  ? spawnSync(`${npmCommand} run ${mode}`, {
      cwd: webRoot,
      shell: true,
      stdio: 'inherit',
    })
  : spawnSync(npmCommand, ['run', mode], {
      cwd: webRoot,
      stdio: 'inherit',
    });

if (result.error) {
  throw result.error;
}

if (result.status !== 0) {
  throw new Error(`apps/web npm run ${mode} failed with exit code ${result.status}.`);
}
