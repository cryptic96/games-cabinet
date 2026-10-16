/**
 * Runs the shared copy cases against the page script's string tables with the plain Node test runner. The same cases file is read
 * by the C# test of the server's labels, so a phrase both stacks show cannot drift between them. The script is read from disk and
 * imported through a data URL.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../../Cabinet.Service/wwwroot/js/copy.js', import.meta.url), 'utf8');
const { copyFor } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const cases = JSON.parse(readFileSync(new URL('./fixtures/filter-copy-cases.json', import.meta.url), 'utf8'));

/**
 * The text a table gives for one case: a function is called with the case's arguments, a plain string is returned as it is.
 * @param {object} table The string table of the case's language.
 * @param {{ key: string, args: unknown[] }} entry The case.
 * @returns {string}
 */
function textFor(table, entry) {
  const value = table[entry.key];

  assert.notEqual(value, undefined, `the page script has no ${entry.key}`);

  return typeof value === 'function' ? value(...entry.args) : value;
}

test('the cases file is not empty and every case names a stack and a language', () => {
  assert.ok(cases.length > 0);

  for (const entry of cases) {
    assert.ok(['js', 'cs', 'both'].includes(entry.stack), `stack of ${entry.key}`);
    assert.ok(['en', 'nl'].includes(entry.language), `language of ${entry.key}`);
    assert.ok(Array.isArray(entry.args), `arguments of ${entry.key}`);
  }
});

for (const entry of cases.filter((candidate) => candidate.stack === 'js' || candidate.stack === 'both')) {
  const label = entry.args.length === 0 ? entry.key : `${entry.key}(${entry.args.map((arg) => JSON.stringify(arg)).join(', ')})`;

  test(`${label} reads as "${entry.text}" in ${entry.language}`, () => {
    assert.equal(textFor(copyFor(entry.language), entry), entry.text);
  });
}
