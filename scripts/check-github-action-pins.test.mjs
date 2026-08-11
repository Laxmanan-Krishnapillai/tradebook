import assert from "node:assert/strict";
import test from "node:test";

import { inspectActionReferences } from "./check-github-action-pins.mjs";

const commit = "0123456789abcdef0123456789abcdef01234567";
const digest = "a".repeat(64);

test("accepts full commit SHAs, Docker digests and local actions", () => {
    const references = inspectActionReferences(`
steps:
  - uses: actions/checkout@${commit} # v4
  - uses: "docker://alpine@sha256:${digest}"
  - uses: ./.github/actions/build
`);

    assert.equal(references.length, 3);
    assert.ok(references.every((reference) => reference.immutable));
});

test("rejects mutable tags, branches, abbreviated SHAs and Docker tags", () => {
    const references = inspectActionReferences(`
steps:
  - uses: actions/checkout@v4
  - uses: owner/action@main
  - uses: owner/action@0123456
  - uses: docker://alpine:3.21
`);

    assert.deepEqual(
        references.map((reference) => reference.immutable),
        [false, false, false, false],
    );
});

test("reports line numbers and ignores unrelated YAML values", () => {
    const references = inspectActionReferences(`
description: "uses: owner/action@v1"
jobs:
  build:
    uses: 'owner/workflow@${commit}'
`);

    assert.deepEqual(references, [
        {
            line: 5,
            reference: `owner/workflow@${commit}`,
            immutable: true,
        },
    ]);
});
