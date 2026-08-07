#!/usr/bin/env node

import { access, readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const repositoryRoot = path.resolve(import.meta.dirname, '..');
const tasksDirectory = path.join(repositoryRoot, 'docs', 'tasks');
const taskFilePattern = /^task-(\d{2})-.*\.md$/;
const markdownLinkPattern = /!?\[[^\]]*\]\(([^)]+)\)/g;

const taskEntries = (await readdir(tasksDirectory))
  .filter((entry) => taskFilePattern.test(entry))
  .sort();
const expectedTaskNumbers = Array.from({ length: 10 }, (_, index) => String(index + 1).padStart(2, '0'));
const actualTaskNumbers = taskEntries.map((entry) => entry.match(taskFilePattern)[1]);

const failures = [];
if (actualTaskNumbers.join(',') !== expectedTaskNumbers.join(',')) {
  failures.push(`expected task specifications 01-10; found ${actualTaskNumbers.join(', ') || 'none'}`);
}

const markdownFiles = [path.join(tasksDirectory, 'README.md'), ...taskEntries.map((entry) => path.join(tasksDirectory, entry))];
let checkedLinks = 0;

for (const markdownFile of markdownFiles) {
  const source = await readFile(markdownFile, 'utf8');

  for (const match of source.matchAll(markdownLinkPattern)) {
    let target = match[1].trim();
    if (target.startsWith('<') && target.endsWith('>')) {
      target = target.slice(1, -1);
    }

    if (!target || target.startsWith('#') || /^[a-z][a-z\d+.-]*:/i.test(target)) {
      continue;
    }

    const targetWithoutFragment = target.split(/[?#]/, 1)[0];
    let decodedTarget;
    try {
      decodedTarget = decodeURIComponent(targetWithoutFragment);
    } catch {
      failures.push(`${path.relative(repositoryRoot, markdownFile)} has an invalid encoded link: ${target}`);
      continue;
    }

    const resolvedTarget = decodedTarget.startsWith('/')
      ? path.join(repositoryRoot, decodedTarget.slice(1))
      : path.resolve(path.dirname(markdownFile), decodedTarget);
    checkedLinks += 1;

    try {
      await access(resolvedTarget);
    } catch {
      failures.push(`${path.relative(repositoryRoot, markdownFile)} -> ${target}`);
    }
  }
}

if (failures.length > 0) {
  console.error('Documentation verification FAILED:');
  for (const failure of failures) {
    console.error(` - ${failure}`);
  }
  process.exitCode = 1;
} else {
  console.log(`Documentation verification PASSED: 10 task specifications and ${checkedLinks} local links resolved.`);
}
