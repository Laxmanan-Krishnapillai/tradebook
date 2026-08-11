#!/usr/bin/env node

import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(import.meta.dirname, "..");
const githubDirectory = path.join(repositoryRoot, ".github");
const fullCommitPattern = /^[^@\s]+@[a-f\d]{40}$/i;
const dockerDigestPattern = /^docker:\/\/[^@\s]+@sha256:[a-f\d]{64}$/i;

export function inspectActionReferences(source) {
    const references = [];
    const lines = source.split(/\r?\n/);

    for (const [index, line] of lines.entries()) {
        const match = line.match(
            /^\s*(?:-\s+)?uses:\s*(?:"([^"]+)"|'([^']+)'|([^\s#]+))/,
        );
        if (match === null) {
            continue;
        }

        const reference = match[1] ?? match[2] ?? match[3];
        references.push({
            line: index + 1,
            reference,
            immutable:
                reference.startsWith("./") ||
                fullCommitPattern.test(reference) ||
                dockerDigestPattern.test(reference),
        });
    }

    return references;
}

async function findYamlFiles(directory) {
    const files = [];
    for (const entry of await readdir(directory, { withFileTypes: true })) {
        const entryPath = path.join(directory, entry.name);
        if (entry.isDirectory()) {
            files.push(...(await findYamlFiles(entryPath)));
        } else if (/\.ya?ml$/i.test(entry.name)) {
            files.push(entryPath);
        }
    }

    return files.sort();
}

async function main() {
    const yamlFiles = await findYamlFiles(githubDirectory);
    const violations = [];
    let referenceCount = 0;

    for (const yamlFile of yamlFiles) {
        const source = await readFile(yamlFile, "utf8");
        for (const result of inspectActionReferences(source)) {
            referenceCount += 1;
            if (!result.immutable) {
                violations.push(
                    `${path.relative(repositoryRoot, yamlFile)}:${result.line} uses ${result.reference}`,
                );
            }
        }
    }

    if (violations.length > 0) {
        console.error("GitHub Action pin verification FAILED:");
        for (const violation of violations) {
            console.error(` - ${violation}`);
        }
        console.error(
            "Pin repository actions to a full 40-character commit SHA and Docker actions to a sha256 digest.",
        );
        process.exitCode = 1;
        return;
    }

    console.log(
        `GitHub Action pin verification PASSED: ${referenceCount} immutable or local references across ${yamlFiles.length} YAML files.`,
    );
}

if (
    process.argv[1] &&
    path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
    await main();
}
