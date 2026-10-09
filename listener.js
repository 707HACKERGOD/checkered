import http from 'http';
import fs from 'fs';
import path from 'path';

const PORT         = Number(process.env.SYNC_PORT || 3000);
const TOKEN        = process.env.SYNC_TOKEN || 'change-me-1234';
const PROJECT_ROOT = path.resolve(process.env.SYNC_ROOT || process.cwd());
const MAX_BODY     = 2 * 1024 * 1024;

// Range-replace blocks: [FILE: x] + optional [REPLACE_FROM]/[REPLACE_TO] + fence
const RANGE_BLOCK_RE =
  /\[FILE:\s*([^\]\r\n]+?)\s*\]\s*(?:\[REPLACE_FROM:\s*([^\r\n]*?)\s*\]\s*)?(?:\[REPLACE_TO:\s*([^\r\n]*?)\s*\]\s*)?```[^\r\n]*\r?\n([\s\S]*?)```/g;

function safeResolve(relPath) {
  const target = path.resolve(PROJECT_ROOT, relPath);
  const rel = path.relative(PROJECT_ROOT, target);
  if (rel === '' || rel.startsWith('..') || path.isAbsolute(rel)) {
    throw new Error(`Refusing to write outside project root: ${relPath}`);
  }
  return target;
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', c => {
      body += c;
      if (body.length > MAX_BODY) { reject(new Error('Payload too large')); req.destroy(); }
    });
    req.on('end', () => resolve(body));
    req.on('error', reject);
  });
}

function replaceRange(original, fromAnchor, toAnchor, newBody) {
  const findOne = (needle, label) => {
    const first = original.indexOf(needle);
    if (first === -1) throw new Error(`${label} not found: "${needle}"`);
    if (original.indexOf(needle, first + needle.length) !== -1)
      throw new Error(`${label} is ambiguous (matches more than once): "${needle}"`);
    return first;
  };

  const start = findOne(fromAnchor, 'REPLACE_FROM');
  const end   = findOne(toAnchor,   'REPLACE_TO');
  if (end < start) throw new Error('REPLACE_TO appears before REPLACE_FROM');

  return original.slice(0, start) + newBody + original.slice(end + toAnchor.length);
}

const server = http.createServer(async (req, res) => {
  res.setHeader('Access-Control-Allow-Origin', 'https://chat.deepseek.com');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') { res.writeHead(204); return res.end(); }

  if (req.method !== 'POST' || req.url !== '/update-files') {
    res.writeHead(404, { 'Content-Type': 'application/json' });
    return res.end(JSON.stringify({ error: 'Not found' }));
  }

  try {
    const payload = JSON.parse((await readBody(req)) || '{}');
    if (TOKEN && payload.token !== TOKEN) {
      res.writeHead(401, { 'Content-Type': 'application/json' });
      return res.end(JSON.stringify({ error: 'Bad token' }));
    }
    if (typeof payload.textData !== 'string') throw new Error('Missing textData');

    const written = [];
    const handled = new Set();

    // Pass 1: range-replace blocks (must have both anchors)
    RANGE_BLOCK_RE.lastIndex = 0;
    let r;
    while ((r = RANGE_BLOCK_RE.exec(payload.textData)) !== null) {
      const [, relRaw, fromAnchor, toAnchor, body] = r;
      const relPath = relRaw.trim().replace(/^[/\\]+/, '');

      if (!fromAnchor || !toAnchor) continue;   // not a range block — leave for pass 2
      if (!relPath) continue;

      const abs = safeResolve(relPath);
      if (!fs.existsSync(abs)) throw new Error(`Range-replace target missing: ${relPath}`);

      const original = fs.readFileSync(abs, 'utf8');
      const merged = replaceRange(original, fromAnchor, toAnchor,
                                  body.replace(/\s+$/, '') + '\n');

      fs.writeFileSync(abs, merged, 'utf8');
      handled.add(relPath);
      written.push(relPath);
      console.log(`[sync] ${relPath}  (range replaced)`);
    }

    // Pass 2: whole-file blocks (no anchors), skipping anything already handled
    RANGE_BLOCK_RE.lastIndex = 0;
    let m;
    while ((m = RANGE_BLOCK_RE.exec(payload.textData)) !== null) {
      const [, relRaw, fromAnchor, toAnchor, body] = m;
      const relPath = relRaw.trim().replace(/^[/\\]+/, '');
      if (!relPath) continue;
      if (fromAnchor && toAnchor) continue;     // was a range block
      if (handled.has(relPath)) continue;

      const abs = safeResolve(relPath);
      fs.mkdirSync(path.dirname(abs), { recursive: true });
      fs.writeFileSync(abs, body.replace(/\s+$/, '') + '\n', 'utf8');
      handled.add(relPath);
      written.push(relPath);
      console.log(`[sync] ${relPath}  (whole file)`);
    }

    console.log(`[sync] done — ${written.length} file(s)`);
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ status: 'success', written }));
  } catch (err) {
    console.error('[sync] error:', err.message);
    res.writeHead(400, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: err.message }));
  }
});

server.listen(PORT, () => {
  console.log(`File Sync Server  →  http://localhost:${PORT}`);
  console.log(`Project root      →  ${PROJECT_ROOT}`);
  console.log(`Token             →  ${TOKEN}`);
});