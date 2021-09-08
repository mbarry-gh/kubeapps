# KubeApps (kap)

![License](https://img.shields.io/badge/license-MIT-green.svg)

## Overview

This is a proof of concept of a CLI to make inner-loop Kubernetes development easier by automating common tasks.

For ideas, feature requests, and discussions, please use GitHub discussions so we can collaborate and follow up.

This Codespace is tested with `zsh` and `oh-my-zsh` - it "should" work with bash but hasn't been fully tested. For the HoL, please use zsh to avoid any issues.

You can run the `dev container` locally and you can also connect to the Codespace with a local version of VS Code.

Please experiment and add any issues to the GitHub Discussion. We LOVE PRs!

## Create your repo

> You must have access to Codespaces as an individual or part of a GitHub Team or GitHub Enterprise Cloud
>
> If you are a member of this GitHub organization, you can skip this step and open with Codespaces

Create your repo from this template and add your application code

- Click the `Use this template` button
- Enter your repo details

## Open with Codespaces

- Click the `Code` button on your repo
- Click `Open with Codespaces`
- Click `New Codespace`
- Choose the `4 core` option

## GitOps repo

- Create a GitOps repo and clone to /workspaces/gitops

- Create the gitops directory

  ```bash

  pushd /workspaces/gitops
  
  # create a placeholder file
  mkdir -p gitops
  touch gitops/.placeholder

  # push changes to GitHub
  git add .
  git commit -am "Initial commit"
  git push

  popd

  ```

## Update `deploy/flux.yaml`

- TODO - automate this
- Change the GitHub repo in `deploy/flux.yaml`
- Change the branch if necessary

## Build and Deploy a K3d Cluster

  ```bash

  # build the cluster
  make create

  ```

## KubeApps (kap) Walkthrough

- todo - write kap docs

### Engineering Docs

- Team Working [Agreement](.github/WorkingAgreement.md)
- Team [Engineering Practices](.github/EngineeringPractices.md)
- CSE Engineering Fundamentals [Playbook](https://github.com/Microsoft/code-with-engineering-playbook)

## How to file issues and get help  

This project uses GitHub Issues to track bugs and feature requests. Please search the existing issues before filing new issues to avoid duplicates. For new issues, file your bug or feature request as a new issue.

For help and questions about using this project, please open a GitHub issue.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services.

Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).

Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.

Any use of third-party trademarks or logos are subject to those third-party's policies.
