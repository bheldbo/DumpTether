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
const childEnvironment = mode === 'dev'
  ? {
      ...process.env,
      VITE_API_BASE_URL: '',
      VITE_PROXY_TARGET: 'http://127.0.0.1:55869',
    }
  : process.env;

if (mode === 'dev') {
  try {
    const response = await fetch('http://127.0.0.1:5173', {
      signal: AbortSignal.timeout(1000),
    });

    if (response.ok) {
      process.stdout.write('Reusing the DumpTether Vite server already running at http://127.0.0.1:5173.\n');
      process.exit(0);
    }
  } catch {
    // No reusable Vite process is ready, so Tauri starts one below.
  }
}

const result = process.platform === 'win32'
  ? spawnSync(process.env.ComSpec ?? 'cmd.exe', ['/d', '/s', '/c', `${npmCommand} run ${mode}`], {
      cwd: webRoot,
      env: childEnvironment,
      stdio: 'inherit',
    })
  : spawnSync(npmCommand, ['run', mode], {
      cwd: webRoot,
      env: childEnvironment,
      stdio: 'inherit',
    });

if (result.error) {
  throw result.error;
}

if (result.status !== 0) {
  throw new Error(`apps/web npm run ${mode} failed with exit code ${result.status}.`);
}
