/* Document editor 1.1.0. Serializes a semantic text tree and DOM ranges to safe Markdown. */

export interface TextNode { tag: string; text?: string; href?: string; children?: TextNode[] }

export function escapeMarkdown(text: string): string {
  return text.replace(/\\/g, '\\\\').replace(/([*_[\]`#>|])/g, '\\$1').replace(/\u00a0/g, ' ');
}

/** Converts the supported semantic tree; formatting and quotations share this path. */
export function toMarkdown(node: TextNode): string {
  const children = node.children ?? [];
  const body = children.map(toMarkdown).join('');
  switch (node.tag) {
    case '#text': return escapeMarkdown(node.text ?? '');
    case 'script': case 'style': case 'img': case 'iframe': return '';
    case 'br': return '\n';
    case 'strong': case 'b': return body.trim() ? `**${body}**` : body;
    case 'em': case 'i': return body.trim() ? `*${body}*` : body;
    case 'code': {
      const raw = (node.text ?? body).replace(/\n/g, ' ');
      const fence = '`'.repeat(Math.max(0, ...(raw.match(/`+/g) ?? []).map(s => s.length)) + 1);
      return `${fence} ${raw} ${fence}`;
    }
    case 'pre': {
      const raw = node.text ?? body;
      const fence = '`'.repeat(Math.max(2, ...(raw.match(/`+/g) ?? []).map(s => s.length)) + 1);
      return `\n\n${fence}\n${raw}\n${fence}\n\n`;
    }
    case 'h1': case 'h2': case 'h3': case 'h4': case 'h5': case 'h6':
      return `\n\n${'#'.repeat(Number(node.tag[1]))} ${body.trim()}\n\n`;
    case 'p': case 'div': return `\n\n${body.trim()}\n\n`;
    case 'blockquote': return '\n\n' + body.trim().split('\n').map(line => '> ' + line).join('\n') + '\n\n';
    case 'ul': case 'ol': return '\n\n' + children.filter(n => n.tag === 'li').map((n, index) => {
      const content = toMarkdown(n).trim().replace(/\n/g, '\n    ');
      return `${node.tag === 'ol' ? `${index + 1}.` : '-'} ${content}`;
    }).join('\n') + '\n\n';
    case 'a': return node.href && /^(https?:|mailto:)/i.test(node.href)
      ? `[${body}](${node.href.replace(/[()\s<>]/g, c => encodeURIComponent(c))})` : body;
    case 'table': {
      const rows: TextNode[] = [];
      const visit = (n: TextNode): void => { if (n.tag === 'tr') rows.push(n); else n.children?.forEach(visit); };
      children.forEach(visit);
      const lines = rows.map(row => '| ' + (row.children ?? []).filter(c => c.tag === 'td' || c.tag === 'th').map(c => toMarkdown(c).trim().replace(/\n+/g, ' ')).join(' | ') + ' |');
      if (lines.length) lines.splice(1, 0, '| ' + (rows[0]?.children ?? []).filter(c => c.tag === 'td' || c.tag === 'th').map(() => '---').join(' | ') + ' |');
      return '\n\n' + lines.join('\n') + '\n\n';
    }
    default: return body;
  }
}

function tree(node: Node): TextNode {
  if (node.nodeType === Node.TEXT_NODE) return { tag: '#text', text: node.textContent ?? '' };
  const tag = node instanceof Element ? node.tagName.toLowerCase() : 'root';
  return { tag, text: node.textContent ?? '', href: node instanceof HTMLAnchorElement ? node.getAttribute('href') ?? '' : '', children: [...node.childNodes].map(tree) };
}

export function markdown(node: Node): string {
  return toMarkdown(tree(node)).replace(/\n{3,}/g, '\n\n').trim();
}
