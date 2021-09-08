# this image is built and updated weekly
# https://github.com/cse-labs/codespaces-images
FROM ghcr.io/cse-labs/k3d:latest

# some images require specific values
ARG USERNAME=vscode
ENV PATH $PATH:/usr/local/go/bin

# install go
RUN wget https://golang.org/dl/go1.17.linux-amd64.tar.gz && \
    rm -rf /usr/local/go && \
    tar -C /usr/local -xzf go1.17.linux-amd64.tar.gz && \
    rm go1.17.linux-amd64.tar.gz

# [Optional] Uncomment this section to install additional OS packages.
# alternatively, you can add commands to on-create.sh and/or post-create.sh
# RUN apt-get -y install --no-install-recommends <your-package-list-here>
