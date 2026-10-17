// Checks the page's in-browser RFC 6570 expander against the official
// uritemplate-test suite (git submodule), so the playground never shows a
// wrong expansion. Run from the repository root: node --test web/tests/uritemplate.test.js
"use strict";
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");
const UT = require("../assets/uritemplate.js");

const SUITE = path.join(__dirname, "..", "..", "test", "Chatter.Rest.UriTemplates.Tests", "uritemplate-test");
const FILES = ["spec-examples.json", "extended-tests.json", "negative-tests.json"];

function run(template, variables) {
  try { return UT.expand(template, variables); } catch (e) { if (e instanceof UT.UTError) return false; throw e; }
}

for (const file of FILES) {
  const full = path.join(SUITE, file);
  test(file, { skip: !fs.existsSync(full) && "submodule not initialized (git submodule update --init)" }, () => {
    const groups = JSON.parse(fs.readFileSync(full, "utf8"));
    // The suite expects associative arrays in insertion order; the library (and the page) sort keys ordinally.
    UT.opts.sortKeys = false;
    try {
      for (const [group, { variables, testcases }] of Object.entries(groups)) {
        for (const [template, expected] of testcases) {
          const actual = run(template, variables);
          const ok = Array.isArray(expected) ? expected.includes(actual) : actual === expected;
          assert.ok(ok, `${group}: ${template} expanded to ${JSON.stringify(actual)}, expected ${JSON.stringify(expected)}`);
        }
      }
    } finally {
      UT.opts.sortKeys = true;
    }
  });
}

test("associative-array keys are sorted ordinally, matching the library", () => {
  const keys = { semi: ";", dot: ".", comma: "," };
  assert.strictEqual(UT.expand("{?keys*}", { keys }), "?comma=%2C&dot=.&semi=%3B");
  assert.strictEqual(UT.expand("{?o*}", { o: { b: "1", B: "2", a: "3" } }), "?B=2&a=3&b=1");
});

test("the page's showcase expansions", () => {
  assert.strictEqual(UT.expand("/orders{?status,page}", { status: "shipped", page: "2" }), "/orders?status=shipped&page=2");
});

test("names inherited from Object.prototype are undefined, not values", () => {
  assert.strictEqual(UT.expand("{?toString,constructor}", {}), "");
  assert.strictEqual(UT.expand("/a{/__proto__}{?hasOwnProperty}", {}), "/a");
});
