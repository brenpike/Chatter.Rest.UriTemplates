# Project site

The GitHub Pages site for Chatter.Rest.UriTemplates: a scroll story that expands one URI template step by step, then a playground for trying your own templates.

It is a static site with no build step:

| Path | Contents |
|---|---|
| `index.html` | Page markup |
| `assets/site.css` | Styles and colour tokens (light and dark) |
| `assets/site.js` | Scroll-driven stage, particle bursts, playground UI |
| `assets/uritemplate.js` | In-browser RFC 6570 Level 4 expander used by the playground. Sorts associative-array keys ordinally, as the library does |
| `tests/uritemplate.test.js` | Checks `uritemplate.js` against the official `uritemplate-test` suite. Not deployed |

## Keeping it current

The copy describes RFC 6570 concepts, the engine's design, and the two packages' purpose. It deliberately avoids version numbers, test counts, file names, and API details beyond `UriTemplate`, `Expand`, `UriTemplateValue.From`, `IUriTemplateFactory.Create`, and `AddUriTemplates`, so routine library changes don't make it stale.

## Preview and test

```bash
git submodule update --init
node --test web/tests/uritemplate.test.js
python3 -m http.server -d web 8000   # then open http://localhost:8000
```

## Deploy

`.github/workflows/pages.yml` tests and deploys the site on pushes to `main` that touch `web/**`, the `uritemplate-test` submodule, or the workflow itself. Pull requests touching those paths run the test only. The package CI/CD workflows are not triggered by site changes. See [docs/development.md](../docs/development.md#project-site--pagesyml).
