# 003 Documentation

**Feature**: [#700](https://github.com/Azure/iotedge-lorawan-starterkit/issues/700)  
**Authors**: Roel Fauconnier  
**Status**: Proposes

## Overview / Problem Statement

Currently the documentation is not very well structured, and content is hard to discover. There is no search except for the built-in GitHub code search. Some of the documentation is spread out across different files.

## Possible solutions

### 1. Structurize the current folders

Bring more structure to the current documentation folders, and use GitHub "markdown-preview" feature. The docs can be searched via GitHunb search function. This also means adding additional links from each document to other documents to make things more discoverable.

### 2. Use GitHub pages

GitHub has a "Pages" functionality which allows you to publish a static website to a predefined URL: `https://projectname.github.io`. It can be published from a branch and/or folder. The folders are limited to `/` or `/docs`.  
The best practice seems to be to create a detached branch or git subtree called `gh-pages` which contains only the static website in a `/docs` folder.

#### 2.1 Use Jekyll static site generator

Jekyll is the default option that Github provides to generate a static website. There are Github Actions available to help you build and publish the static website.  
Jekyll requires you to add specific `yaml` snippets to your documentation to be able to build a static site. Additionally, [it is not fully supported on Windows right now](https://docs.github.com/en/pages/setting-up-a-github-pages-site-with-jekyll/about-github-pages-and-jekyll).  

#### 2.2 Use docFX static site generator

DocFX is an open source tool provided by Microsoft that allows you to generate a static website based on a set of markdown files. Additionally, it can build documentation for a codebase based on the "triple slash" comments (`///`) in .NET code.  
It uses `yaml` files to provide structure to the documentation. There is no built-in support, but there are already some Github Actions available to help automate the publishing of docs.

## Decision

Use DocFX to generate a static website, and use GitHub Actions to publish that to GitHub Pages.  

What needs to be done:

- [ ] Bring structure to the current documentation. This could mean moving files and folders around.
- [ ] Create a main TOC file to provide to overall structure. Potential top-level items: Overview / Architecture / Tools / Samples / Specification LNS
- [ ] Bring in DocFX to generate a static site to a `/docs` folder (this means renaming the current /Docs folder). This folder will not be checked into the repository.
- [ ] Create a Github Action that runs on each push to master (merge of PR) and then builds the code and creates the static website in `/docs`. Then create a detached branch `gh-pages` where the `/docs` gets pushed to. (This means `/docs` is _not_ part of the main repo).
- Setup GitHub pages to deploy from branch `gh-pages` and folder `/docs`.
