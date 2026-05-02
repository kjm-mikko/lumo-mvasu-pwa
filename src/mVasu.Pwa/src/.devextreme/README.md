# DevExtreme license setup

DevExtreme 25.2 ships license keys in two formats:

- **LCX** — the long key copied from the DevExpress Customer Portal
  (starts with `LCXv1…`). This is the *raw* form.
- **LCP** — a compact, build-time-converted form that
  `devextreme/core/config({ licenseKey })` accepts at runtime.

`config({ licenseKey: <LCX> })` is **deprecated** in 25.2 (warning W0000)
and falls back to trial mode, so the orange evaluation banner appears.
Conversion from LCX to LCP must happen *before* runtime.

## First-time setup (one-time per developer)

1. Copy your LCX key (one line, starts with `LCXv1…`) into
   `src/mVasu.Pwa/src/.devextreme/dx-license.txt`. This file is gitignored.
2. From `src/mVasu.Pwa/`, run:

   ```bash
   DevExpress_LicensePath="$(pwd)/src/.devextreme/dx-license.txt" \
     npx devextreme-license --out src/.devextreme/license-key.ts \
     --force --no-gitignore
   ```

   The CLI converts LCX → LCP and writes the result to
   `license-key.ts` (also gitignored).

3. `src/main.ts` already imports from `./.devextreme/license-key` and
   calls `config({ licenseKey })` before bootstrap — no further wiring
   needed.

## Re-running

Re-run the CLI whenever the LCX key changes (new version, renewed
subscription) or after upgrading DevExtreme. A `prebuild` npm script can
automate it later — kept manual for now while the pipeline is being
designed.

## Future migration

When the Azure pipeline + Key Vault are in place, fetch the LCX from
Key Vault during pipeline build, set `DevExpress_License`
(env var) and run the CLI as a build step. The local
`dx-license.txt` and `license-key.ts` can then be removed entirely.
