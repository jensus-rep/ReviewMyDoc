// Builds the browser logic of the components: every components/<name>/<name>.ts,
// except the tests, becomes an ES module <name>.js next to its source, which is
// committed so that demo.html and /components/ work without any tooling. The
// script is an .mjs file because package.json has no "type": "module".
import { readdir, stat } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const componentsRoot = join(repositoryRoot, 'components');

/**
 * Collects the TypeScript sources of all components.
 * Tests (*.test.ts) stay out: they run in Node, not in the browser.
 * @returns {Promise<string[]>} absolute paths, sorted, so the output is stable.
 */
async function collectSources() {
  const entries = await readdir(componentsRoot, { withFileTypes: true });
  const sources = [];

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;

    const folder = join(componentsRoot, entry.name);
    const files = await readdir(folder);
    for (const file of files) {
      if (!file.endsWith('.ts') || file.endsWith('.test.ts') || file.endsWith('.d.ts')) continue;
      const path = join(folder, file);
      if ((await stat(path)).isFile()) sources.push(path);
    }
  }

  return sources.sort();
}

/**
 * Builds one source into an ES module next to it.
 * Not minified and without a source map, because the result is read and
 * reviewed in the repository like a source file.
 * @param {string} source absolute path of a <name>.ts file.
 * @returns {Promise<void>}
 */
async function buildSource(source) {
  await build({
    entryPoints: [source],
    outfile: source.replace(/\.ts$/, '.js'),
    format: 'esm',
    target: 'es2022',
    platform: 'browser',
    // Bundle the editor's private modules so the versioned entry URL invalidates
    // all of them together. Copied Atelier components keep their original build.
    bundle: source.endsWith(join('document-editor', 'document-editor.ts')),
    minify: false,
    sourcemap: false,
    logLevel: 'warning'
  });
}

const sources = await collectSources();
// Explicit .js imports resolve to the generated helpers when those files exist.
// Emit helpers first so the editor bundle always contains this build's code.
const editor = sources.find(source => source.endsWith(join('document-editor', 'document-editor.ts')));
for (const source of sources.filter(source => source !== editor)) {
  await buildSource(source);
}
if (editor) await buildSource(editor);

// No output when there is nothing to do: today no component has a .ts file, and
// a silent run keeps the pipeline log readable.
if (sources.length > 0) {
  const names = sources.map((source) => source.slice(componentsRoot.length + 1).replaceAll('\\', '/'));
  console.log('components built: ' + names.join(', '));
}
