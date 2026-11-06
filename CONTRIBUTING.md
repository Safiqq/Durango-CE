# Contributing & Development Guidelines

> **Project Name**: Durango: Community Edition  
> **Repository**: [Safiqq/Durango-CE](https://github.com/Safiqq/Durango-CE)  
> **Commit Convention**: Conventional Commits (separated, logical, atomic commits)  
> **Security Posture**: Paranoid sandbox containment (VM-only execution)

---

## 1. Automated Git Commit & Workflow Policy

To keep the repository history clean, auditable, and production-ready, all contributors and coding agents must adhere strictly to these rules:

### A. Periodic, Separated Commits (Permanent Policy)
* **Commit Periodically & Automatically**: Work must be committed to git regularly in separated, logical stages. Do not hoard large uncommitted working trees.
* **Separation of Concerns**: Never combine source code fixes, build scripts, assets, and documentation into a single grab-bag commit. Split them cleanly:
  * `fix(render): ...` for visual/graphics fixes (e.g. UV inversion).
  * `feat(ui): ...` for user interface features, rebranding, or localization changes.
  * `build(...): ...` for build scripts, compilation tooling, or packaging.
  * `docs(...): ...` for documentation updates, progress logs, or specifications.
  * `chore(...): ...` for repository tooling, cleanup, or `.gitignore` adjustments.
* **Continuous Push to Remote**: After completing a verified unit of work and committing locally, push immediately to `origin/main` to preserve work remotely.

### B. Conventional Commits Standard
Commit messages must follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:
* `feat`: A new feature or client capability (e.g., `feat(gateway): implement local REST gateway`).
* `fix`: A bug fix or patch (e.g., `fix(render): reset UV rect for static title background`).
* `build`: Changes that affect the build system, scripts, or external dependencies.
* `docs`: Documentation-only changes (e.g., `docs(progress): record M7.2 verification results`).
* `chore`: Maintenance tasks, repo configs, or `.gitignore` adjustments.
* `refactor`: Code refactoring that neither fixes a bug nor adds a feature.

### C. Contributor Attribution & Anonymity
* **Identity**: All project modifications, UI branding, and documentation attribution must be credited under **"Durango Community Contributors"**.
* **No Personal Handles**: Never insert personal developer usernames or individual contributor handles into source code, user-facing UI labels, copyright banners, or release metadata.
* **Upstream Attribution**: Always maintain respectful attribution to foundational community researchers:
  * [SoloCode228](https://github.com/SoloCode228)
  * [ShuuuuShi](https://github.com/ShuuuuShi)

---

## 2. Security & Sandbox Guidelines

1. **PC (Windows x64) First**: Focus entirely on the PC Mono client until decompiled, audited, and hardened. Android is deferred to Milestone M9.
2. **Execute Only Inside VMware**: Never execute upstream or third-party game binaries on the host system. All runtime tests must take place in an isolated Windows 11 VMware virtual machine.
3. **VM Hardening**: Keep VMware Shared Folders, Guest Isolation (clipboard and drag-and-drop), and USB auto-connect **disabled**.
4. **Disposable Snapshots**: Take a clean snapshot before every test session (`before-first-run`) and revert after capturing logs.
5. **Zero Trust Toward Upstream Binaries**: All original binaries (`DurangoLauncher.exe`, unpatched `Assembly-CSharp.dll`) are treated as untrusted until proven otherwise.

---

## 3. Documentation Integrity & Compactness

* **Consistency**: All markdown files across the repository must agree on milestone statuses, version numbers, and file paths.
* **Compact yet Complete**: Avoid bloated duplicate explanations across multiple files; use cross-links to dedicated reference documents.
* **Keep Progress Tracked**: Always update [`docs/PROJECT_PROGRESS.md`](docs/PROJECT_PROGRESS.md) when a milestone or live test produces new findings.
