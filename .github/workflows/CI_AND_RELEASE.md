# Continuous Integration and Release Process

This project has adopted [GitHub Flow](https://guides.github.com/introduction/flow/index.html) for development lifecycle.

Also Continuous Integration (CI) and some routine actions are achieved using [GitHub Actions](https://github.com/features/actions).

## Workflows

There are several workflows to react on different GitHub events:

- [Continuous Integration](./on-push.yml)
  - _Purpose_: Build application and run unit tests to ensure that changes doesn't broke anything.
  - _Run conditions_: Runs on every `push` event to any branch except `master` and on pull requests to master branch.

- [Deploy](./on-deploy.yml)
  - _Purpose_: This is a private deploy to several environment to get functionality is up to date
  - _Run conditions_: Manually triggered workflow

## How to publish new release

1. Manually create a new release following versioning scheme principes [SemVer](https://semver.org/).
3. You have to check release notes in the release draft. It is good practices to describe all changes in the release and add links to the issues for each change.
4. Publish the release.