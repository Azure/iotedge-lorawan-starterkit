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

<!-- markdownlint-disable MD046 -->
!!! warning "Required extensions"
    We are using extensions which are not supported by the mkdocs-material
    container out-of-the-box. There are two ways to deal with this:  

    1. use the [manual approach](#alternate-approach)
    2. Create a custom docker image with the plugin installed:  

    ```dockerfile title="Dockerfile"
    FROM squidfunk/mkdocs-material
    RUN pip install mdx_truly_sane_lists
    ```

    ```bash title="Build and run container"
    # in the directory where your dockerfile is
    docker build . -t mkdocs-material-with-extensions
    docker run --rm -it -p 8000:8000 -v ${PWD}:/docs mkdocs-material-with-extensions
    ```
<!-- markdownlint-enable MD046 -->

### Alternate approach

Install Python and pip, and then the required packages:

```python title="install prerequisites"
pip install mkdocs-material
pip install mdx_truly_sane_lists #required plugin
```

```bash title="run mkdocs"
mkdocs serve
```

## Deployment

For deployment, the additional toolset [`mike`](https://github.com/jimporter/mike)
is used. This tool allows us to deploy multiple versions of the documentation.
There is a [manual GitHub Action](https://github.com/Azure/iotedge-lorawan-starterkit/actions/workflows/publish-docs-new-version.yml)
to deploy a specific version.

## Configuration

The file `mkdocs.yml` provides the main configuration for the website, such as
color and themes, plugins and extension. The `TOC` is also defined in the config
file, under the section `nav`.

!!! warning "TOC is not auto-generated"
    Currently, new pages are not automatically added to the TOC. You will need to
    manually add new pages to the `nav`section of the configuration file.
