// Semantic conversion tests guard the stored format independently of the DOM.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { escapeMarkdown, toMarkdown } from './markdown.ts';

test('literal Markdown punctuation stays literal after rich editing', () => {
  assert.equal(escapeMarkdown('*Preis* [EUR]'), '\\*Preis\\* \\[EUR\\]');
});
test('a heading and a bold phrase retain their semantic formatting', () => {
  assert.equal(toMarkdown({tag: 'h2', children: [{tag: 'strong', children: [{tag:'#text', text:'Ziel'}]}]}).trim(), '## **Ziel**');
});
test('active links and embedded active content are not serialized', () => {
  assert.equal(toMarkdown({tag: 'a', href:'javascript:alert(1)', children:[{tag:'#text', text:'Text'}]}), 'Text');
  assert.equal(toMarkdown({tag:'script', children:[{tag:'#text', text:'alert(1)'}]}), '');
});
test('code fences are longer than any backticks in their contents', () => {
  assert.equal(toMarkdown({tag:'pre', text:'``` example'}).trim(), '````\n``` example\n````');
});
test('tables keep their cells and a Markdown header separator', () => {
  assert.equal(toMarkdown({tag:'table', children:[{tag:'tr', children:[{tag:'th', children:[{tag:'#text', text:'Name'}]}]}, {tag:'tr', children:[{tag:'td', children:[{tag:'#text', text:'Anna'}]}]}]}).trim(), '| Name |\n| --- |\n| Anna |');
});
