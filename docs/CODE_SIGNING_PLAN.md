# Code signing plan

**Status:** Not started. This work is optional. Do it only if the SmartScreen warning causes a real problem for users.

## Problem

`DeskRhythm.exe` has no signature. When a user downloads it and opens it, Windows SmartScreen can show **"Windows protected your PC"**. The user must click **More info → Run anyway**. Some company policies (SmartScreen, AppLocker, WDAC) block unsigned apps.

SmartScreen shows the warning for two reasons:

1. The file has no signature, so the publisher is "Unknown".
2. Microsoft has no reputation data for the file.

A signature fixes reason 1. Reputation grows slowly, as more users download the signed file. No certificate removes the warning immediately. Since March 2024, this is also true for EV certificates.

## Options

| Option | Cost | Notes |
|---|---|---|
| **SignPath Foundation** (selected) | Free | Only for open-source projects. SignPath checks each project by hand. The signature shows "SignPath Foundation" as the publisher. |
| Azure Artifact Signing (old name: Trusted Signing) | About US$10/month | Has an official GitHub Action. Microsoft verifies your identity. Only some countries can use it. |
| OV certificate from a CA (Sectigo, SSL.com, DigiCert, Certum) | About US$100–400/year | The private key must be on a hardware token or in a cloud HSM. For CI, use a cloud HSM. |
| Microsoft Store (MSIX) | Free for individual developers | The Store signs the app. The app must change to MSIX and pass Store certification. This is a large change to how we distribute the app. |
| Company certificate | Free | Only for users inside one company. Users outside that company still get the warning. |

## Selected option: SignPath Foundation

This repo can use it: the repo is public and it has the MIT licence.

Risks and limits:

- The publisher name on the signature is **"SignPath Foundation"**, not the maintainer's name.
- SignPath can refuse a new or small project. If they refuse, the next free option is the Microsoft Store.
- For the first signed releases, SmartScreen can still show a warning until reputation grows. The warning then shows a verified publisher, not "Unknown publisher".
- A person must approve each release signing request in the SignPath web UI.

## Steps

### Steps for the repo owner

An agent cannot do these steps. They need the owner's identity, accounts or approval.

1. Apply at <https://signpath.org/apply>. Give the repo URL. Do step 8 (README section) before you apply, because SignPath reads the README.
2. When SignPath approves, create the SignPath account. Turn on MFA in SignPath **and** in GitHub. The SignPath Foundation terms require MFA for both.
3. In SignPath, add the predefined **GitHub.com trusted build system** and link it to the project.
4. Install the SignPath GitHub App on `ryanbehdad/desk-rhythm`. SignPath uses it to read the audit log.
5. In SignPath, create an **artifact configuration**. `actions/upload-artifact` makes a ZIP file, so the root element must be `<zip-file>` with the exe inside it.
6. In GitHub, go to **Settings → Secrets and variables → Actions**. Add the secret `SIGNPATH_API_TOKEN`. Get the token from SignPath. Record these values from SignPath: **organization ID**, **project slug**, **signing policy slug**, **artifact configuration slug**.
7. For each release, approve the signing request in the SignPath web UI. The workflow log shows the link (`signing-request-web-url` output). Approve it before the wait timeout ends (see step 9).

### Steps an agent can do

8. Add a **Code signing policy** section to `README.md`. SignPath requires it (<https://signpath.org/terms>). It must:
   - give credit: "Free code signing provided by SignPath.io, certificate by SignPath Foundation";
   - name the team roles (committers and reviewers, approvers), with links to the GitHub permission groups. Today the maintainer has all roles;
   - say that a committer must review each pull request from a non-committer;
   - include a privacy statement: the app does not send data over the network. The README "Privacy" section already says this.
9. Change `.github/workflows/build.yml`:
   - Add `actions: read` to the `permissions:` block. The SignPath action needs it to download the artifact.
   - Put the signing steps **after** "Publish single-file exe" and **before** "Checksum". The checksum must be of the signed exe.
   - Upload the unsigned exe with `actions/upload-artifact`. Give it a name that is different from the existing `DeskRhythm-<version>` upload, for example `DeskRhythm-unsigned-<version>`. Two uploads with the same name make the job fail. Use the step's `artifact-id` output.
   - Call `signpath/github-action-submit-signing-request@v3` with these inputs: `api-token`, `organization-id`, `project-slug`, `signing-policy-slug`, `artifact-configuration-slug`, `github-artifact-id`, `wait-for-completion: true`, `output-artifact-directory: dist`.
   - Set `wait-for-completion-timeout-in-seconds` to `3600` or more. The default is 600 seconds (10 minutes). Each release needs a manual approval, so 10 minutes is too short.
   - Run the signing steps only for releases, and only when `SIGNPATH_API_TOKEN` is set. A step `if:` cannot read `secrets.*` directly. Put the secret in a job `env:` variable and test that variable. Then CI continues to pass before SignPath approves the project.
10. Make sure that the exe metadata is set. The terms say: "All signed binaries must have metadata attributes set and enforced", and the product name must be the project name. `src/DeskRhythm/DeskRhythm.csproj` already sets `<Product>Desk Rhythm</Product>`. Make sure that SignPath enforces it (artifact configuration or signing policy).
11. Tag a test release. Check the signature:

    ```powershell
    Get-AuthenticodeSignature .\DeskRhythm.exe
    ```

    `Status` must be `Valid`.
12. Remove the text that says the exe is not signed. Add a link to the code signing policy. The terms require the words "code signing policy" on the home page and on the download page. Change these places:
    - `README.md`: "Download", "Troubleshooting" and "Limitations" sections;
    - `.github/workflows/build.yml`: the release notes text in the "Create GitHub Release" step.

## Push note

A push that changes `.github/workflows/` needs a GitHub token with the `workflow` scope.
