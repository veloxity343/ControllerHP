# Contributing

Thanks for helping out. WattsLeft is small and Windows-only, so most contributions are one of three things: a bug report, results from a controller nobody has tested yet, or a pull request.

Looking for something to work on? [Help wanted](docs/dev/help-wanted.md) lists open ideas, from supporting more devices to a Linux or macOS version, each with where to start.

## Reporting a bug or asking for a feature

[Open an issue](https://github.com/veloxity343/WattsLeft/issues/new/choose) and pick a form. The bug report asks for your controller and connection, and a diagnostics report: right-click the tray icon and choose **Diagnostics**.

[Troubleshooting](docs/wiki/troubleshooting.md#reporting-a-bug) explains how to get each of these. Questions are better asked in [Discussions](https://github.com/veloxity343/WattsLeft/discussions).

For security problems, follow [SECURITY.md](SECURITY.md) instead of opening an issue.

## Working on the code

The [developer docs](docs/dev/README.md) cover building, the [architecture](docs/dev/architecture.md), the [diagnostic flags](docs/dev/diagnostics.md) and [adding a controller](docs/dev/adding-a-controller.md).

There are no automated tests; changes are checked against real devices. In your pull request, say which devices and connections you tried, and if you changed how something looks, attach a `--preview-tray` or `--preview-settings` image.

## Pull requests

- Branch from `main`, and keep each pull request to one change.
- Match the surrounding code: its naming, its layout, and comments that explain *why* rather than *what*.
- The CI build must pass before merging.
- Update the [wiki](docs/wiki/README.md) if you change what users see or which devices are supported.
- Add a line under **Unreleased** in [CHANGELOG.md](CHANGELOG.md) for anything users would notice. It becomes the next release's notes.
- Label it `enhancement`, `bug` or `documentation`, so the release notes group it correctly.

## Code of Conduct

Everyone taking part, in issues, discussions or pull requests, is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md). To report a problem privately, use **Report content** in the **...** menu of the comment, issue or discussion.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
