# Working with the documentation

This documentation site is built using [MkDocs](https://www.mkdocs.org/) and
[mkdocs-material](https://squidfunk.github.io/mkdocs-material/). The static documentation
is built from the `docs/main` [branch](https://github.com/Azure/iotedge-lorawan-starterkit/tree/docs/main) in the GitHub repository.

## Working locally

Checkout the branch that contains the documentation:

```bash title="git worktree"
> git checkout docs/main

# If you want to have `dev` branch and `docs` branch side by side,
# try out git worktree

# from the working folder:
> git worktree add c:/path-to-sources/lorawan.docs docs/main

```

The recommended approach is using `docker` to serve the static site locally:

```bash title="serve documentation locally"
> docker pull squidfunk/mkdocs-material

# in the folder where the `docs/main` branch lives locally:
> docker run --rm -it -p 8000:8000 -v ${PWD}:/docs squidfunk/mkdocs-material
```

Now you can see the site running locally on <http://localhost:8080>. You can change
the port in the `docker run` command.
