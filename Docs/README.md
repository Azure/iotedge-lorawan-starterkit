# Documentation

The documentation for this project is available on <https://azure.github.io/iotedge-lorawan-starterkit>.  

The code for the documentation lives in a detached branch called `docs/main`.
This branch is protected and only accepts PR merges. You can checkout this branch
by `git checkout docs/main`.

The documentation static site is built by a GitHub Action, and pushes the content
to another detached branch called `gh-pages`. This branch is configured to
be published to GitHub Pages automatically when it changes.

## Using Git Worktree

It is possible to checkout multiple branches to different folders on your local
development machine. To do this, use the `git worktree` command:

```powershell
# for example, if your repo lives in c:\lora\lorawan, you can checkout the docs
# branch to another folder, e.g. c:\lora\lorawan.docs

> git worktree add c:\lora\lorawan.docs docs/main

```
