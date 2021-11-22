# Working with the documentation

This documentation site is built using [MkDocs](https://www.mkdocs.org/) and
[mkdocs-material](https://squidfunk.github.io/mkdocs-material/). These tools
generate a static website based on a configuration file and a set of markdown
files in the `docs/main` [branch](https://github.com/Azure/iotedge-lorawan-starterkit/tree/docs/main).

`docs/main` is a detached branch that is locked and only accepts PRs. Two Actions
are triggered by the PR:

- On PR creation: Markdown linting and link checking
- On [PR merge](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/publish-docs-dev.yml): deployment of `dev` version of docs website

## Working locally

Checkout the branch that contains the documentation:

```bash title="git worktree"
git checkout docs/main

# If you want to have `dev` branch and `docs` branch side by side,
# try out git worktree

# from the working folder:
git worktree add c:/path-to-sources/lorawan.docs docs/main

```

The recommended approach is using `docker` to serve the static site locally:

```bash title="serve documentation locally"
docker pull squidfunk/mkdocs-material

# in the folder where the `docs/main` branch lives locally:
docker run --rm -it -p 8000:8000 -v ${PWD}:/docs squidfunk/mkdocs-material
```

Now you can see the site running locally on <http://localhost:8000>. You can change
the port in the `docker run` command.

### Alternate approach

Install Python and pip, and then the required packages:

```bash
pip install mkdocs
pip install mkdocs-material
pip install mike
pip install mkdocs-git-revision-date-localized-plugin #optional plugin
```

## Deployment

For deployment, the additional toolset [`mike`](https://github.com/jimporter/mike)
is used. This tool allows us to deploy multiple versions of the documentation.
There is a [manual GitHub Action](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/publish-docs-new-version.yml)
to deploy a specific version.

## configuration

The file `mkdocs.yml` provides the main configuration for the website, such as
color and themes, plugins and extension. The `TOC` is also defined in the config
file, under the section `nav`. Currently, new pages are not automatically added
to the TOC.
