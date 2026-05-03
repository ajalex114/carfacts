/**
 * prebuild.mjs — any pre-build tasks that need to run before `next build`.
 * Currently: loads .env.local for local dev (GitHub Actions provides env vars directly).
 */

import { readFileSync, existsSync } from "fs";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));

// Load .env.local so this script has access to env vars when run via `npm run prebuild`
const envFile = join(__dirname, "../.env.local");
if (existsSync(envFile)) {
  for (const line of readFileSync(envFile, "utf8").split(/\r?\n/)) {
    const match = line.match(/^([^#=\s][^=]*)=(.*)$/);
    if (match) process.env[match[1].trim()] ??= match[2].trim();
  }
}

console.log("[prebuild] done");

