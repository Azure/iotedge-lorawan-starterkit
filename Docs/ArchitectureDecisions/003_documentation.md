# 003 Documentation

**Feature**: [#700](https://github.com/Azure/iotedge-lorawan-starterkit/issues/700)  
**Authors**: Roel Fauconnier  
**Status**: Proposes

## Overview / Problem Statement

Currently the documentation is not very well structured, and content is hard to
discover. There is no search except for the built-in GitHub code search. Some of
the documentation is spread out across different files.

We want to bring the documentation up-to-date and make it more discoverable.

## Proposed solution

1. **Use GitHub Pages**.
GitHub has a "Pages" functionality which allows you to publish a static website
to a predefined URL: `https://projectname.github.io`. It can be published from a
branch and/or folder. The folders are limited to `/` or `/docs`. The best practice
seems to be to create a detached branch or git subtree called `gh-pages` which
contains only the static website in a `/docs` folder.

1. **Use DocFX static site generator**.
[DocFX](https://dotnet.github.io/docfx/) is an open source tool provided by
Microsoft that allows you to generate a static website based on a set of
markdown files. Additionally, it can build documentation for a codebase based on
the "triple slash" comments (`///`) in .NET code.  
It uses `yaml` files to provide structure to the documentation. There is no
built-in support in GitHub, but there are already some GitHub Actions available
to help automate the publishing of docs. However, we will not use the code
documentation feature of DocFX, since the code does not represent an API.

1. **Create a detached branch to keep all docs**, called `docs/main`.
This keeps history out of the main code repository, and allows for changes to
the documentation without triggering any tag, version or CI.

1. **Create a detached branch to keep static site**, called `gh-pages`. This
allows for a place where the docs can be published from.

## Open Questions

- [x] Can multiple branches be locked in GitHub? E.g. ideally `docs/main` will be
locked and can only be updated through a PR.
  * **YES** - it is possible to add rules to different branches.

## Alternatives Considered

1. Structurize the current folders
Bring more structure to the current documentation folders, and use GitHub
"markdown-preview" feature. The docs can be searched via GitHub search function.
This also means adding additional links from each document to other documents to
make things more discoverable.

1. Use Jekyll static site generator
Jekyll is the default option that Github provides to generate a static website.
There are Github Actions available to help you build and publish the static
website. Jekyll requires you to add specific `yaml` snippets to your documentation
to be able to build a static site. Also,
[it is not fully supported on Windows right now](https://docs.github.com/en/pages/setting-up-a-github-pages-site-with-jekyll/about-github-pages-and-jekyll).  

1. Use a different repository for the docs
While this is a solution for some OSS repositories, it is overkill for this one.
Additionally, it may be hard to open a new repository within the Azure organization.
